#!/bin/sh
# Database backups. "loop": runs in the backup container, one dump every night at 2:30 (TZ is Asia/Kolkata).
# "now": one dump straight away (deploy.sh runs this before each release). Old dumps go after KEEP_DAYS days.
set -eu

dump() {
  file="/backups/grocery_khata-$(date +%Y-%m-%d_%H%M).dump"
  pg_dump -h db -U postgres -d grocery_khata -Fc -Z 6 -f "$file.part"
  mv "$file.part" "$file"
  find /backups -name 'grocery_khata-*.dump' -mtime +"${KEEP_DAYS:-14}" -delete
  echo "backup: $file ($(du -h "$file" | cut -f1))"
}

case "${1:-now}" in
  now) dump ;;
  loop)
    while true; do
      now=$(date +%s)
      next=$(date -d "$(date +%Y-%m-%d) 02:30" +%s 2>/dev/null || date -D '%Y-%m-%d %H:%M' -d "$(date +%Y-%m-%d) 02:30" +%s)
      [ "$next" -le "$now" ] && next=$((next + 86400))
      sleep $((next - now))
      dump || echo "backup: FAILED"
    done
    ;;
esac
