#!/usr/bin/env bash
# One-time setup of a fresh Ubuntu 24.04 VPS for Grocery Khata. Run as root:
#   sudo bash setup-server.sh app.example.in you@example.com
# Installs Nginx, PostgreSQL 17, certbot, age and rclone; creates the khata user, the database and its login
# (not a superuser, so row-level security applies), the secrets file, the service and HTTPS.
# Safe to run again: steps that are already done are skipped.
set -euo pipefail
DOMAIN="${1:?usage: setup-server.sh <domain> <email for certificate notices>}"
EMAIL="${2:?usage: setup-server.sh <domain> <email for certificate notices>}"
HERE="$(cd "$(dirname "$0")" && pwd)"

echo "== Packages"
apt-get update -q
apt-get install -yq curl ca-certificates gnupg nginx certbot python3-certbot-nginx age rclone ufw
if ! command -v psql >/dev/null || ! psql --version | grep -q " 17"; then
  install -d /usr/share/postgresql-common/pgdg
  curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc
  echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $(. /etc/os-release && echo "$VERSION_CODENAME")-pgdg main" \
    > /etc/apt/sources.list.d/pgdg.list
  apt-get update -q
  apt-get install -yq postgresql-17
fi

echo "== Firewall: SSH and web only"
ufw allow OpenSSH >/dev/null
ufw allow 'Nginx Full' >/dev/null
ufw --force enable >/dev/null

echo "== User and folders"
id khata >/dev/null 2>&1 || useradd --system --home /opt/grocery-khata --shell /usr/sbin/nologin khata
install -d -o khata -g khata /opt/grocery-khata /opt/grocery-khata/api /opt/grocery-khata/web /opt/grocery-khata/scripts
install -d -m 700 /etc/grocery-khata /var/backups/grocery-khata

echo "== Database"
if [ ! -f /etc/grocery-khata/env ]; then
  DB_PASSWORD="$(openssl rand -hex 24)"
  JWT_KEY="$(openssl rand -hex 32)"
  sudo -u postgres psql -v ON_ERROR_STOP=1 -q <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'khata_app') THEN
    CREATE ROLE khata_app LOGIN PASSWORD '$DB_PASSWORD' NOSUPERUSER NOCREATEROLE NOCREATEDB;
  ELSE
    ALTER ROLE khata_app PASSWORD '$DB_PASSWORD';
  END IF;
END \$\$;
SQL
  sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='grocery_khata'" | grep -q 1 \
    || sudo -u postgres createdb -O khata_app grocery_khata
  sed -e "s|Password=<generated>|Password=$DB_PASSWORD|" -e "s|<generated: 64 random characters>|$JWT_KEY|" \
    "$HERE/env.example" > /etc/grocery-khata/env
  chmod 600 /etc/grocery-khata/env
  echo "   Secrets written to /etc/grocery-khata/env. Add your phone number and service keys there."
else
  echo "   /etc/grocery-khata/env exists: left as it is."
fi

echo "== Service"
install -m 644 "$HERE/grocery-khata.service" /etc/systemd/system/grocery-khata.service
systemctl daemon-reload
systemctl enable grocery-khata >/dev/null

echo "== Nginx and HTTPS"
sed "s/app.example.in/$DOMAIN/g" "$HERE/nginx-grocery-khata.conf" > /etc/nginx/sites-available/grocery-khata
ln -sf /etc/nginx/sites-available/grocery-khata /etc/nginx/sites-enabled/grocery-khata
rm -f /etc/nginx/sites-enabled/default
if [ ! -d "/etc/letsencrypt/live/$DOMAIN" ]; then
  # certbot needs plain HTTP first; the site file's 443 block gets its certificate lines from certbot.
  systemctl stop nginx || true
  certbot certonly --standalone -d "$DOMAIN" -m "$EMAIL" --agree-tos --non-interactive
fi
grep -q ssl_certificate /etc/nginx/sites-available/grocery-khata || sed -i \
  "s|listen 443 ssl http2;|listen 443 ssl http2;\n    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;\n    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;|" \
  /etc/nginx/sites-available/grocery-khata
nginx -t
systemctl restart nginx

echo
echo "Done. Next:"
echo " 1. Edit /etc/grocery-khata/env: your phone (Platform__AdminPhones__0) and any service keys."
echo " 2. From your PC: scripts/deploy.ps1 -Server root@<this server>"
