#!/bin/bash
# Sends the current commit's source to the server and builds and runs it there. From the repository root
# (Git Bash on Windows works):
#   deploy/production/deploy.sh root@SERVER
# Steps: pack the committed source (a few MB, not the images), copy it and the Six Labors key over SSH, build both
# images on the server (tagged with the git commit), update the stack's files, take a database backup, start the new
# release, and check it answers. Building on the server means a slow home upload only carries the source.
set -euo pipefail

server="${1:?usage: deploy/production/deploy.sh user@server}"
root="$(git rev-parse --show-toplevel)"
licence="${SIXLABORS_LICENCE:-$root/deploy/demo/sixlabors.lic}"
tag="$(git -C "$root" rev-parse --short HEAD)"
remote=/opt/grocery-khata
build="/opt/grocery-khata-build/$tag"

if [ -n "$(git -C "$root" status --porcelain --untracked-files=no -- backend frontend deploy/production ':!**/appsettings.Development.json')" ]; then
  echo "backend/, frontend/ or deploy/production/ has uncommitted changes: commit first, so the release matches a commit." >&2
  exit 1
fi
[ -s "$licence" ] || { echo "Six Labors key file not found or empty: $licence (or set SIXLABORS_LICENCE)" >&2; exit 1; }

echo "== Sending the source of $tag to $server"
ssh "$server" "rm -rf '$build' && mkdir -p '$build'"
git -C "$root" archive --format=tar.gz HEAD backend frontend deploy/production | ssh "$server" "tar -xzf - -C '$build'"
scp -q "$licence" "$server:$build/sixlabors.lic"

ssh "$server" bash -s -- "$tag" "$remote" "$build" <<'REMOTE'
set -euo pipefail
tag="$1"; remote="$2"; build="$3"
trap 'rm -rf "$build"' EXIT  # the key and the source never stay on the server

echo "== Building release $tag on the server"
cd "$build"
DOCKER_BUILDKIT=1 docker build -q -f deploy/production/Dockerfile.api --secret id=sixlabors,src=sixlabors.lic -t "grocery-khata-api:$tag" backend
DOCKER_BUILDKIT=1 docker build -q -f deploy/production/Dockerfile.web -t "grocery-khata-web:$tag" frontend

echo "== Updating the stack's files"
cp deploy/production/grocery-khata/{docker-compose.yml,nginx.conf,init-db.sh,backup.sh} "$remote/"

cd "$remote"
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
    echo "== Release $tag is up"; docker image prune -f >/dev/null; docker builder prune -f --filter until=168h >/dev/null; exit 0
  fi
  sleep 4
done
echo "!! The API did not answer in 2 minutes. Logs: docker compose logs api --tail 100" >&2
echo "!! To go back: sed -i 's/^TAG=.*/TAG=$previous/' .env && docker compose up -d (and restore the backup if the database changed)" >&2
exit 1
REMOTE
