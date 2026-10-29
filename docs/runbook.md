# Grocery Khata runbook

For whoever is on call. Server: one Hostinger VPS running Nginx, the API (systemd `grocery-khata`), and PostgreSQL 17.

## First-time server setup

On a fresh Ubuntu 24.04 VPS, with the domain's DNS A record already pointing at it:

1. Copy the `deploy/` folder up: `scp -r deploy root@<server>:/root/`
2. `ssh root@<server> "bash /root/deploy/setup-server.sh <domain> <email>"`. This installs Nginx, PostgreSQL 17, certbot,
   age and rclone, opens only SSH and web in the firewall, creates the database and its non-superuser login, writes
   `/etc/grocery-khata/env` with generated secrets, installs the service and gets the HTTPS certificate.
3. Edit `/etc/grocery-khata/env`: your phone number (`Platform__AdminPhones__0`), backup settings, and any service keys.

## Deploy

From the PC: `.\scripts\deploy.ps1 -Server root@<server> -Domain <domain>`. It runs the tests, builds the app and a
self-contained Linux API plus a migration bundle, uploads them, takes a backup (when configured), applies migrations,
swaps in the new version, restarts and checks `/api/health/ready`. If the check fails it puts the previous version back.
A migration is not undone by that: migrations only add, so the previous version still runs on the newer database.

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
| Ledger balance check | `/api/v1/ledger/reconcile`, per shop | Any mismatch (the nightly check at 3 am logs an error and shows the owner) |
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
- Platform admins see shop details (owner, staff, counts), not customers or khata entries. Every admin view, approval,
  block and unblock writes a row to that shop's own audit log (table `platform_admin`), which its owner can see.

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
| 29 Oct, with AI ordering, staff, OTP and platform admin (API not pinned to 1 core) | 25 × 2 | 29.7 | 176 ms overall, bill 600 ms, p99 4.6 s | Pass overall; bill p95 over |
| 29 Oct, after batching a bill's stock update and dropping its read-back (same setup) | 25 × 2 | 30.3 | 76 ms overall, bill 82 ms | Pass. p99 of 1.5–4 s on every action, reads included, so the tail is this laptop (load generator, console logs), not bill locks: recheck on staging |
| 29 Oct, same code, console logging at Warning, two runs back to back | 25 × 2 | 31.5 / 31.9 | 75 ms, then 562 ms | Pass, then fail. Quieter logs didn't remove the 1–3 s tail, and the same code swings from 75 to 562 ms: this laptop (Docker on Windows, everything on one machine) can't judge it. Decide the server size from a staging run only |

At 25 × 2, the API used about 26% of its core and Postgres was nearly idle. Its sessions were waiting on the client, not on
locks or disk, and turning off synchronous commit didn't help. So the slow tail comes from this laptop (the load generator,
Docker/WSL and the API all share one machine), not the database. This test can't settle KVM 1 vs KVM 2. Run the command
above against the real VPS before launch.

**Sizing decision:** start on **KVM 2** (2 vCPU, 8 GB). The cost difference is small, and it leaves room for Postgres and
the API to run side by side during the evening peak. Drop to KVM 1 only if the VPS test passes there with p95 under 500 ms at
25 × 2.

## Known gaps

- Privacy policy and terms are drafts and need review by a lawyer (DPDP Act 2023) before launch.
