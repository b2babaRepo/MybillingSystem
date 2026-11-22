#!/bin/bash
# One-time setup of a fresh Ubuntu 24.04 VPS (e.g. KVM 4), run as root on the server:
#   scp -r deploy/production root@SERVER:/opt/apps-setup && ssh root@SERVER 'bash /opt/apps-setup/server-setup.sh'
# Installs Docker, a firewall (SSH, HTTP, HTTPS only), automatic security updates and swap; makes the shared "edge"
# network; puts the proxy in /opt/proxy and Grocery Khata in /opt/grocery-khata with every secret generated.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"

echo "== Docker"
if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | sh
fi
systemctl enable --now docker

echo "== Firewall: SSH, HTTP, HTTPS"
apt-get update -qq && apt-get install -y -qq ufw unattended-upgrades >/dev/null
ufw default deny incoming >/dev/null
ufw default allow outgoing >/dev/null
ufw allow OpenSSH >/dev/null && ufw allow 80/tcp >/dev/null && ufw allow 443/tcp >/dev/null && ufw allow 443/udp >/dev/null
ufw --force enable >/dev/null
dpkg-reconfigure -f noninteractive unattended-upgrades

echo "== 4 GB swap (a cushion when apps peak together)"
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
  sysctl -w vm.swappiness=10 >/dev/null && echo 'vm.swappiness=10' > /etc/sysctl.d/99-swap.conf
fi

echo "== Shared network for the proxy and every app's website"
docker network inspect edge >/dev/null 2>&1 || docker network create edge >/dev/null

echo "== Proxy in /opt/proxy"
mkdir -p /opt/proxy
cp -rn "$here/proxy/." /opt/proxy/
[ -f /opt/proxy/.env ] || cp /opt/proxy/.env.example /opt/proxy/.env

echo "== Grocery Khata in /opt/grocery-khata"
mkdir -p /opt/grocery-khata/backups
cp -n "$here/grocery-khata/docker-compose.yml" "$here/grocery-khata/nginx.conf" "$here/grocery-khata/init-db.sh" \
      "$here/grocery-khata/backup.sh" "$here/grocery-khata/.env.example" /opt/grocery-khata/
if [ ! -f /opt/grocery-khata/.env ]; then
  gen() { openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-48; }
  sed -e "s|^DB_ADMIN_PASSWORD=.*|DB_ADMIN_PASSWORD=$(gen)|" \
      -e "s|^DB_APP_PASSWORD=.*|DB_APP_PASSWORD=$(gen)|" \
      -e "s|^Jwt__SigningKey=.*|Jwt__SigningKey=$(gen)$(gen)|" \
      -e "s|^Agents__AadhaarHashKey=.*|Agents__AadhaarHashKey=$(gen)|" \
      -e "s|^Billing__BillLinkKey=.*|Billing__BillLinkKey=$(gen)|" \
      /opt/grocery-khata/.env.example > /opt/grocery-khata/.env
fi
chmod 600 /opt/grocery-khata/.env /opt/proxy/.env

cat <<'NEXT'

Done. Next:
  1. Point your domain's DNS A record at this server.
  2. Edit /opt/proxy/.env (ACME_EMAIL, GK_DOMAIN) and /opt/grocery-khata/.env (your admin phone, SMS, payments...).
     Keep a copy of /opt/grocery-khata/.env somewhere safe.
  3. Start the proxy:  cd /opt/proxy && docker compose up -d
  4. From your PC:     deploy/production/deploy.sh root@THIS_SERVER
NEXT
