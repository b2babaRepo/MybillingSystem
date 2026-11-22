# Production with Docker (KVM 4 VPS, several apps)

Grocery Khata runs on one VPS next to your other apps. One **Caddy** proxy takes ports 80 and 443 for every
app and handles HTTPS by itself; each app is its own Docker Compose stack with memory limits, so one app can't
starve the others.

```
internet ─► Caddy (proxy, :80 :443, HTTPS) ─edge network─► grocery-khata-web (nginx: site + /api)
                                                         └► other apps' websites
grocery-khata internal network:  web ─► api (.NET) ─► db (Postgres 17)      backup ─► db (nightly dump)
```

Nothing but Caddy is reachable from the internet. The images are built on your PC (or in CI), never on the server.

## Memory budget on a KVM 4 (16 GB, 4 vCPU)

| Container | Limit | Notes |
|---|---|---|
| proxy / caddy | 256 MB | shared by all apps |
| grocery-khata / db | 4 GB, 2 CPU | `shared_buffers=1GB`, `effective_cache_size=3GB` |
| grocery-khata / api | 1.5 GB, 2 CPU | .NET heap capped at 75% of the limit |
| grocery-khata / web | 128 MB | nginx |
| grocery-khata / backup | 256 MB | `pg_dump` at 2:30 every night |
| **Grocery Khata total** | **about 6 GB** | |
| Ubuntu, Docker | about 1 GB | plus 4 GB swap as a cushion |
| **Free for other apps** | **about 9 GB** | |

Grow the db limit (and `shared_buffers`, `effective_cache_size`) when the database outgrows it.

## First time

1. **Server.** Buy the VPS with Ubuntu 24.04, then from your PC:
   ```bash
   scp -r deploy/production root@SERVER:/opt/apps-setup
   ssh root@SERVER 'bash /opt/apps-setup/server-setup.sh'
   ```
   This installs Docker, a firewall (SSH, HTTP, HTTPS only), automatic security updates and swap. It also
   makes the shared `edge` network and puts the stacks in `/opt/proxy` and `/opt/grocery-khata`, with every
   secret generated.
2. **Domain.** Point your domain's DNS **A record** at the server's IP.
3. **Settings.** On the server, edit `/opt/proxy/.env` (`ACME_EMAIL`, `GK_DOMAIN`) and
   `/opt/grocery-khata/.env` (your admin phone, SMS, payments, AI). **Keep a copy of
   `/opt/grocery-khata/.env` somewhere safe**: losing its keys signs everyone out and breaks old bill links.
4. **Proxy.** `cd /opt/proxy && docker compose up -d`
5. **First release.** From the repository root on your PC (Git Bash works on Windows):
   ```bash
   deploy/production/deploy.sh root@SERVER
   ```
   The database is created on the first start, and the app applies its migrations.

## Every release

```bash
deploy/production/deploy.sh root@SERVER
```

It builds the images for the current commit, copies them to the server over SSH (no registry needed), backs up
the database, starts the new release and checks `/api/health/ready`. If the check fails, it prints how to go
back to the previous release.

The Six Labors (ImageSharp) licence key is read from `deploy/demo/sixlabors.lic`, or from the file in the
`SIXLABORS_LICENCE` environment variable. It is used only while building and never goes into an image.

## Backups

- A dump every night at 2:30 (India time) in `/opt/grocery-khata/backups`, kept 14 nights (`BACKUP_KEEP_DAYS`),
  plus one before every release.
- **Copy them off the server.** A backup on the same VPS is lost with the VPS. For example, from your PC:
  `scp root@SERVER:/opt/grocery-khata/backups/*.dump ./backups/`. Or sync them to cloud storage with rclone.
- Restore (stops the app first):
  ```bash
  cd /opt/grocery-khata && docker compose stop api
  docker compose exec -T db pg_restore -U postgres -d grocery_khata --clean --if-exists < backups/FILE.dump
  docker compose start api
  ```

## Adding another app

1. Give its website container the `edge` network and a unique alias (like `grocery-khata-web` here).
2. Add `/opt/proxy/sites/<app>.caddy` with its domain and `reverse_proxy <alias>:<port>`.
3. `cd /opt/proxy && docker compose restart caddy`
4. Give it memory limits, and keep its database private to its own stack (or add a database and role to
   Grocery Khata's Postgres if you'd rather share one).

## Useful commands (on the server)

```bash
cd /opt/grocery-khata
docker compose ps                    # what's running
docker compose logs api --tail 100   # API logs
docker stats --no-stream             # memory and CPU per container
docker compose exec backup /bin/sh /backup.sh now   # a backup right now
```
