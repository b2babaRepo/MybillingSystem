#!/bin/bash
# Builds a release on this PC and runs it on the server. From the repository root (Git Bash on Windows works):
#   deploy/production/deploy.sh root@SERVER
# Steps: build both images (tagged with the git commit), copy them to the server over SSH (no registry needed),
# update the stack's files, take a database backup, start the new release, and check it answers.
set -euo pipefail

server="${1:?usage: deploy/production/deploy.sh user@server}"
root="$(git rev-parse --show-toplevel)"
here="$root/deploy/production"
licence="${SIXLABORS_LICENCE:-$root/deploy/demo/sixlabors.lic}"
tag="$(git -C "$root" rev-parse --short HEAD)"
remote=/opt/grocery-khata

if [ -n "$(git -C "$root" status --porcelain --untracked-files=no -- backend frontend ':!**/appsettings.Development.json')" ]; then
  echo "backend/ or frontend/ has uncommitted changes: commit first, so the release matches a commit." >&2
  exit 1
fi
[ -s "$licence" ] || { echo "Six Labors key file not found or empty: $licence (or set SIXLABORS_LICENCE)" >&2; exit 1; }

echo "== Building release $tag"
DOCKER_BUILDKIT=1 docker build -q -f "$here/Dockerfile.api" --secret id=sixlabors,src="$licence" -t "grocery-khata-api:$tag" "$root/backend"
DOCKER_BUILDKIT=1 docker build -q -f "$here/Dockerfile.web" -t "grocery-khata-web:$tag" "$root/frontend"

echo "== Copying images to $server"
docker save "grocery-khata-api:$tag" "grocery-khata-web:$tag" | gzip -1 | ssh "$server" 'gunzip | docker load -q'

echo "== Updating the stack's files"
scp -q "$here/grocery-khata/docker-compose.yml" "$here/grocery-khata/nginx.conf" "$here/grocery-khata/init-db.sh" \
       "$here/grocery-khata/backup.sh" "$server:$remote/"

ssh "$server" bash -s -- "$tag" "$remote" <<'REMOTE'
set -euo pipefail
tag="$1"; cd "$2"
previous=$(grep -E '^TAG=' .env | cut -d= -f2)
if docker compose ps --status running --services 2>/dev/null | grep -q '^db$'; then
  echo "== Backup before the release (the new one may change the database)"
  docker compose exec -T backup /bin/sh /backup.sh now </dev/null
fi
sed -i "s/^TAG=.*/TAG=$tag/" .env
echo "== Starting release $tag (was ${previous:-none})"
docker compose up -d --remove-orphans </dev/null
for i in $(seq 1 30); do
  if docker compose exec -T web wget -qO- http://api:8080/api/health/ready </dev/null >/dev/null 2>&1; then
    echo "== Release $tag is up"; docker image prune -f >/dev/null; exit 0
  fi
  sleep 4
done
echo "!! The API did not answer in 2 minutes. Logs: docker compose logs api --tail 100" >&2
echo "!! To go back: sed -i 's/^TAG=.*/TAG=$previous/' .env && docker compose up -d (and restore the backup if the database changed)" >&2
exit 1
REMOTE
