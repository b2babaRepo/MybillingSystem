# Builds and deploys Grocery Khata to the VPS from this PC.
#   .\scripts\deploy.ps1 -Server root@203.0.113.10 -Domain app.example.in
# Builds the app and a self-contained API for Linux, plus a migration bundle; copies them up; backs up the database
# (if backups are configured); applies migrations; swaps in the new version; restarts; and checks /api/health/ready.
# If the check fails, the previous version is put back.
param(
    [Parameter(Mandatory)] [string] $Server,
    [Parameter(Mandatory)] [string] $Domain
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'out\deploy'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

Write-Host '== Tests'
dotnet test (Join-Path $root 'backend') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed: not deploying.' }

Write-Host '== App'
Push-Location (Join-Path $root 'frontend')
npm ci --no-audit --no-fund
npm run build
npm run budget
if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'Frontend build or budget failed.' }
Pop-Location
Copy-Item -Recurse (Join-Path $root 'frontend\dist') (Join-Path $out 'web')

Write-Host '== API (Linux, self-contained) and migration bundle'
$api = Join-Path $root 'backend\src\GroceryKhata.Api'
dotnet publish $api -c Release -r linux-x64 --self-contained -o (Join-Path $out 'api') --nologo -v q
dotnet ef migrations bundle --project $api -r linux-x64 --self-contained -o (Join-Path $out 'efbundle') --force
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $root 'scripts\backup.sh') $out

Write-Host '== Upload'
tar -C $out -czf (Join-Path $root 'out\release.tgz') .
scp (Join-Path $root 'out\release.tgz') "${Server}:/tmp/grocery-khata-release.tgz"

Write-Host '== Install on the server'
$remote = @'
set -euo pipefail
cd /tmp && rm -rf gk-new && mkdir gk-new && tar -xzf grocery-khata-release.tgz -C gk-new
set -a; . /etc/grocery-khata/env; set +a
if [ -n "${BACKUP_AGE_RECIPIENT:-}" ]; then PGURL="${BACKUP_PGURL}" bash gk-new/backup.sh; else echo "WARNING: backups not configured (BACKUP_AGE_RECIPIENT) - no backup taken"; fi
chmod +x gk-new/efbundle gk-new/api/GroceryKhata.Api
./gk-new/efbundle --connection "$ConnectionStrings__Default"
rm -rf /opt/grocery-khata/api.prev /opt/grocery-khata/web.prev
mv /opt/grocery-khata/api /opt/grocery-khata/api.prev && mv gk-new/api /opt/grocery-khata/api
mv /opt/grocery-khata/web /opt/grocery-khata/web.prev && mv gk-new/web /opt/grocery-khata/web
install -m 755 gk-new/backup.sh /opt/grocery-khata/scripts/backup.sh
chown -R khata:khata /opt/grocery-khata
systemctl restart grocery-khata
for i in $(seq 1 30); do
  if curl -fsS "https://__DOMAIN__/api/health/ready" | grep -q '"ready"'; then echo "LIVE: https://__DOMAIN__"; exit 0; fi
  sleep 2
done
echo "Health check failed: putting the previous version back"
rm -rf /opt/grocery-khata/api /opt/grocery-khata/web
mv /opt/grocery-khata/api.prev /opt/grocery-khata/api && mv /opt/grocery-khata/web.prev /opt/grocery-khata/web
systemctl restart grocery-khata
journalctl -u grocery-khata -n 30 --no-pager
exit 1
'@
$remote = $remote.Replace('__DOMAIN__', $Domain).Replace("`r`n", "`n")
$remote | ssh $Server 'bash -s'
if ($LASTEXITCODE -ne 0) { throw 'Deploy failed on the server (previous version restored). See the log above.' }
