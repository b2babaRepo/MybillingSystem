# Grocery Khata runbook

For whoever is on call. Server: one Hostinger VPS running Nginx, the API (systemd `grocery-khata`), and PostgreSQL 17.

## Deploy

1. Run a backup: `scripts/backup.sh`. Never migrate without a fresh one.
2. Copy the published API to `/opt/grocery-khata/api` and `frontend/dist` to `/opt/grocery-khata/web`.
3. Migrate: `dotnet ef database update` (with `Database__MigrateOnStartup=false` in production).
4. `systemctl restart grocery-khata`, then `curl -f https://<domain>/api/health/ready` should return `{"status":"ready","pendingMigrations":0}`.

The API refuses to start in production with the dev signing key, with `Database__SeedDemo` on, or with a superuser
DB login, which would skip row-level security. If it won't start, `journalctl -u grocery-khata -n 50` gives the reason.

Secrets (`Jwt__SigningKey`, `ConnectionStrings__Default`, backup keys) live only in `/etc/grocery-khata/env` (mode 600).
They are never stored in the repo.

## Monitoring and alerts

| Check | Tool | Alert when |
|---|---|---|
| `GET /api/health/ready` every minute | UptimeRobot / Better Stack (free tier) | 2 failures in a row |
| Backup heartbeat (`BACKUP_HEARTBEAT_URL`) | Same monitor, heartbeat type | No ping for 26 h |
| Disk free | `node_exporter` or a cron `df` check | Under 20% |
| API 5xx rate | Nginx access log (`goaccess`, or Loki later) | Over 1% for 5 min |
| p95 latency | Nginx `$request_time` | Over 500 ms for 10 min, evenings 6 to 10 pm |
| Ledger balance check | `/api/v1/ledger/reconcile`, per shop | Any mismatch (job not built yet; see below) |
| Certificate expiry | Monitor's SSL check | Under 14 days |

Alerts go to the owner's phone (the monitor's app or Telegram) and to email.

## Incidents

- **API down:** check `systemctl status grocery-khata`, then the journal, then restart. Shops keep billing offline, and the
  outbox syncs when the API is back, so say so in the WhatsApp status rather than panicking anyone.
- **DB down or full disk:** free space (old logs, `/var/backups` beyond 14 nights) and restart Postgres. Never delete WAL by hand.
- **Wrong data reported by a shop:** ledger rows are never edited. Find the entry in `/audit` and fix it with a reversal.
- **Suspected leak between shops:** take the API down (writes stay queued on phones). Check RLS with
  `SELECT relrowsecurity FROM pg_class WHERE relname='customers'` and that the login is not a superuser. Then follow the DPDP
  breach steps in `privacy-policy.md`.

## Backups and restore drills

`scripts/backup.sh` runs nightly. It takes a `pg_dump`, encrypts it with `age` to a public key (the private key is kept off
the server, in the owner's password manager and on paper), and copies it off the server with `rclone`. It keeps 14 nights.

Run a restore drill every quarter: `scripts/restore-drill.sh <file> <key>` on a laptop. Record it here:

| Date | Backup | Rows matched | Time to restore | By |
|---|---|---|---|---|
| | | | | |

The second drill required by the plan should be done on the VPS's first real backup before launch.

## Per-shop data requests

- Export: the owner downloads everything from Settings (`GET /api/v1/shop/export`).
- Deletion: the owner types the shop's name (`POST /api/v1/shop/delete`). This removes all of the shop's rows. Backups age out
  within 14 days. Tell the shop this.
- No platform-admin role exists yet, so nobody can read a shop's data through the app. When one is added, every admin read
  must write an audit row; that is what "logged admin access" in the plan means.

## Load test (evening rush)

`node loadtest/evening-rush.mjs --base https://staging.<domain> --shops 25 --cashiers 2 --seconds 300`, with the sign-up
and global rate limits raised on staging only.

Local results from 27 Oct 2026. They were measured on a Windows laptop, with the API pinned to 1 core, Postgres limited to
1 CPU, and the load generator on the same machine:

| Run | Load | req/s | p95 | Result |
|---|---|---|---|---|
| Before the fix | 25 shops × 2 | 26.6 | 1137 ms | Fail |
| After one connection per request | 25 × 2 | 29.1 | 552–603 ms | Fail (bill p99 3.5–5.9 s) |
| After one connection per request | 10 × 2 | 12.3 | 403 ms | Pass |

At 25 × 2, the API used about 26% of its core and Postgres was nearly idle. Its sessions were waiting on the client, not on
locks or disk, and turning off synchronous commit didn't help. So the slow tail comes from this laptop (the load generator,
Docker/WSL and the API all share one machine), not the database. This test can't settle KVM 1 vs KVM 2. Run the command
above against the real VPS before launch.

**Sizing decision:** start on **KVM 2** (2 vCPU, 8 GB). The cost difference is small, and it leaves room for Postgres and
the API to run side by side during the evening peak. Drop to KVM 1 only if the VPS test passes there with p95 under 500 ms at
25 × 2.

## Known gaps

- PIN sign-in doesn't exist yet. Password sign-in is rate-limited per IP (`Auth:AuthRequestsPerMinutePerIp`). Add
  per-account lockout when PINs arrive.
- There is no nightly job that reconciles every shop yet.
- Privacy policy and terms are drafts and need review by a lawyer (DPDP Act 2023) before launch.
