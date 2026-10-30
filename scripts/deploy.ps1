# Builds and deploys Grocery Khata to your VPS (Hostinger KVM, Ubuntu 24.04) from this PC, in one command:
#   .\scripts\deploy.ps1 -Server 203.0.113.10 -Domain grocerykhata.in -Email you@gmail.com -AdminPhone 9876543210
# First run on a fresh server: sets up SSH-key login (you type the server's root password once), then the server
# itself (deploy/setup-server.sh: database, secrets, HTTPS, firewall, backups), and saves the backup key to this PC.
# Every run: tests, builds the app and a self-contained Linux API plus a migration bundle, uploads them, backs up the
# database, applies migrations, swaps in the new version, restarts and checks /api/health/ready; if the check fails,
# the previous version is put back.
#   -FetchBackups   only copy the server's encrypted backups to this PC (out\backups), nothing else.
param(
    [Parameter(Mandatory)] [string] $Server,
    [Parameter(Mandatory)] [string] $Domain,
    [string] $Email,
    [string] $AdminPhone,
    [string] $User = 'root',
    [switch] $FetchBackups,
    [switch] $SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = "$User@$Server"
$sshOpts = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'ServerAliveInterval=30')

function Remote([string] $script) {
    $script.Replace("`r`n", "`n") | ssh @sshOpts $target 'bash -s'
    if ($LASTEXITCODE -ne 0) { throw "Step failed on the server (exit $LASTEXITCODE). See the log above." }
}

Write-Host '== SSH key login'
$key = Join-Path $HOME '.ssh\id_ed25519'
if (-not (Test-Path $key)) {
    New-Item -ItemType Directory -Force (Split-Path $key) | Out-Null
    ssh-keygen -t ed25519 -N '""' -f $key -q
}
# A refused key login writes to stderr, which Windows PowerShell would turn into an error here: allow it for this check.
$ErrorActionPreference = 'Continue'
ssh @sshOpts -o BatchMode=yes $target 'true' 2>$null
$keyWorks = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
if (-not $keyWorks) {
    Write-Host '   Type the server root password (from Hostinger hPanel > VPS) once; after this, logins use your key.'
    Get-Content "$key.pub" | ssh @sshOpts $target 'mkdir -p ~/.ssh && chmod 700 ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys'
    if ($LASTEXITCODE -ne 0) { throw 'Could not log in to the server. Check the IP and the root password.' }
}

if ($FetchBackups) {
    $dest = Join-Path $root 'out\backups'
    New-Item -ItemType Directory -Force $dest | Out-Null
    scp @sshOpts "${target}:/var/backups/grocery-khata/*.age" $dest
    Write-Host "Backups copied to $dest (encrypted; your key: $HOME\GroceryKhata-backup-key.txt)"
    return
}

Write-Host '== Server setup (first run only)'
ssh @sshOpts $target 'test -f /etc/grocery-khata/env'
$isNew = $LASTEXITCODE -ne 0
if ($isNew) {
    if (-not $Email -or -not $AdminPhone) { throw 'First deploy: also give -Email (for the HTTPS certificate) and -AdminPhone (your login as platform admin).' }
    tar -C $root -czf (Join-Path $root 'out\setup.tgz') deploy scripts/backup.sh
    scp @sshOpts (Join-Path $root 'out\setup.tgz') "${target}:/tmp/gk-setup.tgz"
    Remote "set -e; rm -rf /tmp/gk-setup && mkdir /tmp/gk-setup && tar -xzf /tmp/gk-setup.tgz -C /tmp/gk-setup && bash /tmp/gk-setup/deploy/setup-server.sh '$Domain' '$Email' '$AdminPhone'"
    # The backup key's private half comes to this PC and leaves the server.
    $backupKey = Join-Path $HOME 'GroceryKhata-backup-key.txt'
    scp @sshOpts "${target}:/root/grocery-khata-backup-key.txt" $backupKey
    if ($LASTEXITCODE -eq 0) {
        ssh @sshOpts $target 'shred -u /root/grocery-khata-backup-key.txt'
        Write-Host "   Backup key saved to $backupKey. Keep a copy somewhere safe (Google Drive, pen drive): without it, backups can't be opened."
    }
}

$out = Join-Path $root 'out\deploy'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

if (-not $SkipTests) {
    Write-Host '== Tests'
    dotnet test (Join-Path $root 'backend') --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed: not deploying.' }
}

Write-Host '== App'
Push-Location (Join-Path $root 'frontend')
npm ci --no-audit --no-fund
npm run build
if ($LASTEXITCODE -eq 0) { node scripts/check-budget.mjs }
if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'Frontend build or size budget failed.' }
Pop-Location
Copy-Item -Recurse (Join-Path $root 'frontend\dist') (Join-Path $out 'web')

Write-Host '== API (Linux, self-contained) and migration bundle'
$api = Join-Path $root 'backend\src\GroceryKhata.Api'
dotnet publish $api -c Release -r linux-x64 --self-contained -o (Join-Path $out 'api') --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
dotnet ef migrations bundle --project $api -r linux-x64 --self-contained -o (Join-Path $out 'efbundle') --force
if ($LASTEXITCODE -ne 0) { throw 'Migration bundle failed.' }
Copy-Item (Join-Path $root 'scripts\backup.sh') $out

Write-Host '== Upload'
tar -C $out -czf (Join-Path $root 'out\release.tgz') .
scp @sshOpts (Join-Path $root 'out\release.tgz') "${target}:/tmp/grocery-khata-release.tgz"
if ($LASTEXITCODE -ne 0) { throw 'Upload failed.' }

Write-Host '== Install on the server'
$remote = @'
set -euo pipefail
cd /tmp && rm -rf gk-new && mkdir gk-new && tar -xzf grocery-khata-release.tgz -C gk-new
set -a; . /etc/grocery-khata/env; set +a
install -m 755 gk-new/backup.sh /opt/grocery-khata/scripts/backup.sh
if [ -n "${BACKUP_AGE_RECIPIENT:-}" ] && [ -n "${BACKUP_PGURL:-}" ] && sudo -u postgres psql -tAc "SELECT 1 FROM pg_tables WHERE tablename='shops'" grocery_khata | grep -q 1; then
  PGURL="${BACKUP_PGURL}" /opt/grocery-khata/scripts/backup.sh
fi
chmod +x gk-new/efbundle gk-new/api/GroceryKhata.Api
./gk-new/efbundle --connection "$ConnectionStrings__Default"
rm -rf /opt/grocery-khata/api.prev /opt/grocery-khata/web.prev
mv /opt/grocery-khata/api /opt/grocery-khata/api.prev && mv gk-new/api /opt/grocery-khata/api
mv /opt/grocery-khata/web /opt/grocery-khata/web.prev && mv gk-new/web /opt/grocery-khata/web
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
journalctl -u grocery-khata -n 40 --no-pager
exit 1
'@
Remote $remote.Replace('__DOMAIN__', $Domain)
Write-Host "Done: https://$Domain"
