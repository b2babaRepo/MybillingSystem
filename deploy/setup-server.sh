#!/usr/bin/env bash
# One-time setup of a fresh Ubuntu 24.04 VPS (Hostinger KVM or any other) for Grocery Khata. Run as root — normally
# by scripts/deploy.ps1, which uploads this folder and runs it on the first deploy:
#   sudo bash setup-server.sh app.example.in you@example.com 9876543210
# Installs Nginx, PostgreSQL 17, certbot, age, fail2ban and automatic security updates; adds swap on small servers;
# creates the khata user, the database and its login (not a superuser, so row-level security applies), a backup login,
# every secret (random), the service, HTTPS and nightly encrypted backups.
# Safe to run again: steps that are already done are skipped.
set -euo pipefail
DOMAIN="${1:?usage: setup-server.sh <domain> <email for certificate notices> <your mobile number>}"
EMAIL="${2:?usage: setup-server.sh <domain> <email> <mobile>}"
ADMIN_PHONE="${3:?usage: setup-server.sh <domain> <email> <mobile>}"
HERE="$(cd "$(dirname "$0")" && pwd)"
export DEBIAN_FRONTEND=noninteractive

echo "== Domain check: $DOMAIN must point to this server"
MY_IP="$(curl -fsS -4 https://api.ipify.org || hostname -I | awk '{print $1}')"
DNS_IP="$(getent ahostsv4 "$DOMAIN" | awk 'NR==1{print $1}' || true)"
if [ "$DNS_IP" != "$MY_IP" ]; then
  echo "   $DOMAIN points to '${DNS_IP:-nothing}', this server is $MY_IP."
  echo "   In Hostinger hPanel > Domains > DNS, set an A record for $DOMAIN to $MY_IP, wait a few minutes and run again."
  exit 2
fi

echo "== Packages"
apt-get update -q
apt-get install -yq curl ca-certificates gnupg nginx certbot python3-certbot-nginx age rclone ufw fail2ban unattended-upgrades
if ! command -v psql >/dev/null || ! psql --version | grep -q " 17"; then
  install -d /usr/share/postgresql-common/pgdg
  curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc
  echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $(. /etc/os-release && echo "$VERSION_CODENAME")-pgdg main" \
    > /etc/apt/sources.list.d/pgdg.list
  apt-get update -q
  apt-get install -yq postgresql-17
fi
# Security updates install themselves; fail2ban blocks SSH password guessing.
dpkg-reconfigure -f noninteractive unattended-upgrades >/dev/null
systemctl enable --now fail2ban >/dev/null

echo "== Swap (small servers run out of memory during busy hours without it)"
if ! swapon --show | grep -q .; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "== Firewall: SSH and web only"
ufw allow OpenSSH >/dev/null
ufw allow 'Nginx Full' >/dev/null
ufw --force enable >/dev/null

echo "== User and folders"
id khata >/dev/null 2>&1 || useradd --system --home /opt/grocery-khata --shell /usr/sbin/nologin khata
install -d -o khata -g khata /opt/grocery-khata /opt/grocery-khata/api /opt/grocery-khata/web /opt/grocery-khata/scripts
install -d -m 700 /etc/grocery-khata /var/backups/grocery-khata

echo "== Database and secrets"
if [ ! -f /etc/grocery-khata/env ]; then
  DB_PASSWORD="$(openssl rand -hex 24)"
  BACKUP_PASSWORD="$(openssl rand -hex 24)"
  sudo -u postgres psql -v ON_ERROR_STOP=1 -q <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'khata_app') THEN
    CREATE ROLE khata_app LOGIN PASSWORD '$DB_PASSWORD' NOSUPERUSER NOCREATEROLE NOCREATEDB;
  ELSE
    ALTER ROLE khata_app PASSWORD '$DB_PASSWORD';
  END IF;
  -- Backups must read every shop's rows: row-level security would hide them from khata_app.
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'khata_backup') THEN
    CREATE ROLE khata_backup LOGIN PASSWORD '$BACKUP_PASSWORD' NOSUPERUSER BYPASSRLS;
  ELSE
    ALTER ROLE khata_backup PASSWORD '$BACKUP_PASSWORD';
  END IF;
END \$\$;
GRANT pg_read_all_data TO khata_backup;
SQL
  sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='grocery_khata'" | grep -q 1 \
    || sudo -u postgres createdb -O khata_app grocery_khata

  # Backups are encrypted to a key made here; scripts/deploy.ps1 copies the private half to your PC and removes it
  # from the server, so a stolen server can't read old backups.
  age-keygen -o /root/grocery-khata-backup-key.txt 2>/dev/null
  chmod 600 /root/grocery-khata-backup-key.txt
  RECIPIENT="$(age-keygen -y /root/grocery-khata-backup-key.txt)"

  sed -e "s|Password=<generated>|Password=$DB_PASSWORD|" \
      -e "s|Jwt__SigningKey=.*|Jwt__SigningKey=$(openssl rand -hex 32)|" \
      -e "s|Agents__AadhaarHashKey=.*|Agents__AadhaarHashKey=$(openssl rand -hex 32)|" \
      -e "s|Billing__BillLinkKey=.*|Billing__BillLinkKey=$(openssl rand -hex 32)|" \
      -e "s|Platform__AdminPhones__0=.*|Platform__AdminPhones__0=$ADMIN_PHONE|" \
      -e "s|BACKUP_PGURL=.*|BACKUP_PGURL=postgresql://khata_backup:$BACKUP_PASSWORD@localhost/grocery_khata|" \
      -e "s|BACKUP_AGE_RECIPIENT=.*|BACKUP_AGE_RECIPIENT=$RECIPIENT|" \
      "$HERE/env.example" > /etc/grocery-khata/env
  chmod 600 /etc/grocery-khata/env
  echo "   Secrets written to /etc/grocery-khata/env (random, never shown)."
else
  echo "   /etc/grocery-khata/env exists: left as it is."
fi

echo "== Nightly backup at 2:30 (server time)"
install -m 755 "$HERE/../scripts/backup.sh" /opt/grocery-khata/scripts/backup.sh 2>/dev/null || install -m 755 "$HERE/backup.sh" /opt/grocery-khata/scripts/backup.sh
cat > /etc/cron.d/grocery-khata-backup <<'CRON'
30 2 * * * root set -a; . /etc/grocery-khata/env; set +a; PGURL="$BACKUP_PGURL" /opt/grocery-khata/scripts/backup.sh >> /var/log/grocery-khata-backup.log 2>&1
CRON

echo "== Service"
install -m 644 "$HERE/grocery-khata.service" /etc/systemd/system/grocery-khata.service
systemctl daemon-reload
systemctl enable grocery-khata >/dev/null

echo "== Nginx and HTTPS"
sed "s/app.example.in/$DOMAIN/g" "$HERE/nginx-grocery-khata.conf" > /etc/nginx/sites-available/grocery-khata
ln -sf /etc/nginx/sites-available/grocery-khata /etc/nginx/sites-enabled/grocery-khata
rm -f /etc/nginx/sites-enabled/default
if [ ! -d "/etc/letsencrypt/live/$DOMAIN" ]; then
  # certbot needs plain HTTP first; the site file's 443 block gets its certificate lines below.
  systemctl stop nginx || true
  certbot certonly --standalone -d "$DOMAIN" -m "$EMAIL" --agree-tos --non-interactive
  # Renewals (certbot's timer) briefly stop and start Nginx the same way.
  echo "pre_hook = systemctl stop nginx" >> "/etc/letsencrypt/renewal/$DOMAIN.conf"
  echo "post_hook = systemctl start nginx" >> "/etc/letsencrypt/renewal/$DOMAIN.conf"
fi
grep -q ssl_certificate /etc/nginx/sites-available/grocery-khata || sed -i \
  "s|listen 443 ssl http2;|listen 443 ssl http2;\n    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;\n    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;|" \
  /etc/nginx/sites-available/grocery-khata
nginx -t
systemctl restart nginx

echo "SETUP DONE"
