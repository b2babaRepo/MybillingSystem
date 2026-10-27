# Grocery Khata

Billing and khata (customer credit ledger) for kirana shops. Many shops on one server, each one's data kept apart.
Phase 1 walking skeleton from `Grocery Khata Project Plan.docx`: sign up a shop → add a product → bill a customer on
khata → see the statement, plus the amount-only khata entry and the offline outbox, built in from day one.

| Part | Stack |
|---|---|
| `backend/` | ASP.NET Core on .NET 10, EF Core 10 + PostgreSQL (Npgsql), JWT auth |
| `frontend/` | React 19 + TypeScript + Vite, installable PWA (Workbox), TanStack Query, IndexedDB (`idb`), Tailwind CSS, i18next (Hindi/English) |
| `docker-compose.yml` | Local PostgreSQL 17 |

## Run it locally

Needs the .NET 10 SDK, Node.js 22+ and Docker Desktop.

```powershell
docker compose up -d                                   # PostgreSQL on localhost:5432
dotnet run --project backend/src/GroceryKhata.Api      # API on http://localhost:5274 (creates the tables on start)
cd frontend; npm install; npm run dev                  # app on http://localhost:5173 (proxies /api to the API)
```

Open http://localhost:5173 on your phone's browser (same Wi-Fi, use the PC's IP with `npm run dev -- --host`) or in
Chrome's device mode. "Open a new shop" signs you up.

Tests:

```powershell
dotnet test backend          # integration tests against a real PostgreSQL in Docker (Testcontainers)
cd frontend; npm test        # outbox, merge and keypad unit tests
```

## How the pieces fit

### Many shops, one database (ported from SchoolErp)

- One person (a `users` row, by mobile number) can own or work in many shops through `memberships` (owner or staff).
- Sign-in returns a **shop token** at once when the person has one shop; with several, a user token plus the list,
  then `POST /api/v1/auth/select-shop`. The shop comes **only** from the token's `shop_id` claim, never from a request.
- Access tokens last 30 minutes. Refresh tokens last 90 days (a shop phone can be offline for days), rotate on every
  use, and are stored only as hashes. Reusing an old refresh token ends all of that person's sessions.
- Five wrong passwords lock the account for 15 minutes; sign-in endpoints are rate-limited per IP.
- **Three guards** keep shops apart:
  1. an EF Core query filter on every shop-owned table (`ITenantOwned`);
  2. a save interceptor that stamps `shop_id` on inserts and blocks cross-shop updates;
  3. PostgreSQL **row-level security**. Every connection sets `app.shop_id`, and policies on `products`,
     `customers`, `ledger_entries`, `bills` and `bill_lines` hide and refuse other shops' rows. The app connects as
     `khata_app`, a non-superuser (superusers skip RLS).
- Links between shop-owned tables are two-column foreign keys `(shop_id, id)`, so the database refuses a Shop A
  entry pointing at a Shop B customer.

### The khata ledger

- `ledger_entries` is append-only. Corrections are reversal entries (`POST /api/v1/ledger-entries/{id}/reverse`); an
  entry can be reversed once. Nothing is deleted.
- Amounts are signed: + means the customer owes more (udhaar), − means they paid (jama). Money is `numeric(18,2)`,
  quantities `numeric(12,3)`.
- `customers.balance` is a cache of the sum of entries, changed atomically in the same transaction as each entry.
  `GET /api/v1/ledger/reconcile` (owner only) lists any customer where they differ, and a test proves it stays empty.
- **Amount-only entry**: `POST /api/v1/khata-entries` with `{ id, customerId, type: credit|payment, amount }`.
  This is the default path in the app: pick the customer, type the amount, tap Udhaar or Jama.
- **Bills**: `POST /api/v1/bills`. With a customer, the total posts as a credit and anything paid now posts as a
  payment. A walk-in sale must be paid in full.

### Offline-first: the outbox

- Every write in the app (customer, khata entry, bill, reversal, product) is saved to IndexedDB first, shown at once,
  and sent in order by `frontend/src/lib/outbox.ts`, on start-up, when the connection returns, and every 30 seconds.
- Each record gets its ID on the phone (UUID v7). The server keeps it as the row's primary key, so a retry or a second
  sync returns the saved record (`200`) instead of creating a duplicate.
- What you see = the last server data (also cached in IndexedDB for opening offline) + unsynced ops on top
  (`frontend/src/lib/local.ts`). Unsynced rows carry an amber dot.
- An op the server refuses (400/404/409) is marked failed and shown on the **Sync** tab with the reason; the rest
  carry on. A network failure or server error stops the queue, to retry later.
- The app asks the browser for persistent storage and warns before closing or signing out with unsynced entries.
- **Undo** (30 s after saving) drops the entry if it hasn't synced yet, otherwise posts a reversal.

## API

| Method | Path | |
|---|---|---|
| POST | `/api/v1/auth/sign-up` | Open a shop (an existing phone adds a shop to that account, with its password) |
| POST | `/api/v1/auth/sign-in` | Phone + password |
| POST | `/api/v1/auth/select-shop` | Pick one of several shops |
| POST | `/api/v1/auth/refresh` · `/sign-out` | Rotate / revoke the refresh token |
| GET | `/api/v1/auth/me` | User and their shops |
| GET/POST | `/api/v1/products` | Catalogue |
| GET/POST | `/api/v1/customers` | Customers with balances (`?search=`) |
| GET | `/api/v1/customers/{id}/statement` | Entries with running balance |
| POST | `/api/v1/khata-entries` | Amount-only udhaar / jama |
| POST | `/api/v1/bills` | Itemised cash memo, paid or on khata |
| POST | `/api/v1/ledger-entries/{id}/reverse` | Correction |
| GET | `/api/v1/ledger/reconcile` | Balance check (owner) |
| GET | `/api/health` | Uptime check |

## Configuration

`backend/src/GroceryKhata.Api/appsettings.json`. Outside Development, set these as environment variables:

- `ConnectionStrings__Default`: as the `khata_app` role, never a superuser.
- `Jwt__SigningKey`: at least 32 random characters. The Development key in `appsettings.Development.json` is public.
- `Database__MigrateOnStartup`: `false` in production. Run migrations at deploy, after a backup:
  `dotnet ef database update --project backend/src/GroceryKhata.Api`.

## Not in the skeleton yet

- Staff invites and the platform-admin role (the SchoolErp invite flow ports over directly), PIN sign-in, customer OTP.
- Stock movements, stocktake, CSV import and the starter catalogue (Phase 2).
- Cash book screen, month-end statement PDF and the overdue list (Phases 3 and 6).
- A nightly background job that runs the reconciliation for every shop. The check exists; the job doesn't yet.
- PNG app icons (192/512) for older Android install prompts. Only the SVG icon is included.
- Nginx, systemd and the deploy step for the Hostinger VPS (CI builds and tests; it does not deploy).
