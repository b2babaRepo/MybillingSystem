#!/usr/bin/env bash
# Restore drill: restore a backup into a throwaway database and check it is complete.
#   scripts/restore-drill.sh khata-2026-10-27T0230Z.dump.age ~/.age/khata.key
# Run it on a laptop or a spare box, never into the live database.
set -euo pipefail
backup=$1; key=$2
db=khata_restore_$(date +%s)
createdb "$db"
age -d -i "$key" "$backup" | pg_restore --no-owner --dbname="$db"
psql -d "$db" -v ON_ERROR_STOP=1 <<'SQL'
SELECT 'shops' AS t, count(*) FROM shops UNION ALL
SELECT 'customers', count(*) FROM customers UNION ALL
SELECT 'ledger_entries', count(*) FROM ledger_entries UNION ALL
SELECT 'bills', count(*) FROM bills;
SQL
echo "Restored into $db. Compare the counts with production, point a local API at it and open /api/v1/ledger/reconcile as an owner (balances must match the ledger), then: dropdb $db"
