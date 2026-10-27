#!/usr/bin/env bash
# Nightly encrypted backup (cron: 30 2 * * *  /opt/grocery-khata/scripts/backup.sh)
# Dumps the database, encrypts it with the public key in BACKUP_AGE_RECIPIENT (age), keeps 14 nights
# on the server and copies each one off the server. The private key is NOT on the server.
set -euo pipefail
: "${PGURL:?postgres URL of a role that can read every row (the owner, not khata_app: RLS would hide shops)}"
: "${BACKUP_AGE_RECIPIENT:?age public key}"
: "${BACKUP_REMOTE:?rclone remote, e.g. b2:khata-backups}"
dir=/var/backups/grocery-khata
mkdir -p "$dir"
file="$dir/khata-$(date -u +%Y-%m-%dT%H%MZ).dump.age"
pg_dump --format=custom --no-owner "$PGURL" | age -r "$BACKUP_AGE_RECIPIENT" > "$file"
rclone copy "$file" "$BACKUP_REMOTE"
find "$dir" -name 'khata-*.dump.age' -mtime +14 -delete
# Tell the uptime monitor the backup ran (a missed ping raises the "backup missed" alert).
[ -n "${BACKUP_HEARTBEAT_URL:-}" ] && curl -fsS -m 10 "$BACKUP_HEARTBEAT_URL" > /dev/null
echo "backup ok: $file"
