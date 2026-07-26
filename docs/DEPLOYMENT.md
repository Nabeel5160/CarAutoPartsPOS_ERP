# Deployment runbook — Car Auto Parts ERP

## Topology

- **SQL Server** (LocalDB / full SQL Server): single database, multi-company rows
- **CarAutoParts.Api**: Kestrel or IIS reverse-proxy; JWT + health at `/health`
- **CarAutoParts.Web**: Blazor WASM static host (IIS/nginx/CDN) pointing at API
- **CarAutoParts.Presentation**: WPF POS on counter PCs (same API or local SQL)

## Prerequisites

1. .NET 8 runtime / hosting bundle (IIS)
2. SQL Server with a login that can create DB / run migrations
3. Connection string in `appsettings.json` / environment:
   `ConnectionStrings__DefaultConnection=Server=...;Database=CarAutoPartsDb;...`

## First-time deploy

1. Publish API: `dotnet publish src/CarAutoParts.Api -c Release -o ./publish/api`
2. Publish Web: `dotnet publish src/CarAutoParts.Web -c Release -o ./publish/web`
3. Set `Jwt:Key` to a long random secret (min 32 chars)
4. Set `Cors:AllowedOrigins` to **every** Web origin you use (localhost + LAN IP + production host). Example: `http://192.168.x.x:5156`. If the PC’s LAN IP changes, update CORS or browsers will block API calls.
5. Start API once — `MigrateAsync` + seed create demo company, COA, fiscal periods, admin user
6. Login: **admin** / **admin123** — the UI **forces** a password change before other screens (`MustChangePassword`)
7. Map COA via Finance → Chart of Accounts; verify Account Mappings for Sales/Purchase/GRN (seed upserts core maps + GL `1400`; startup throws if required maps are missing)

### LAN access (dev)

- Bind API/Web to `http://0.0.0.0:5280` / `:5156` (use `--no-launch-profile` so launchSettings does not force localhost-only).
- Blazor rewrites `ApiBaseUrl` from `localhost:5280` to `{same-host}:5280` when the page is opened via a non-localhost host.
- Open firewall for TCP 5156 and 5280 if other devices cannot connect.

### POS / FBR integrity

- Checkout commits invoice + stock + payment + GL in one DB transaction.
- FBR is attempted after commit. **FBR failure does not roll back the sale**; a failed submission is stored and retried via outbox / FBR page.
- Sale is source of truth; FBR is async compliance.
- **Cashiers must open a shift** before POS checkout (Settings/roles need `pos.shift`).
- Sales returns require a **reason code** and post GL via `SalesReturn` account mappings.
- **Phase 2:** Purchase requisitions → PO; GRN over-receive / QC hold / serials; 3-way match tolerances; supplier returns with stock+GL; reorder suggestions. Migration `Phase2Procurement` applies on API start.
- **Phase 3:** ATP (`/api/inventory/atp`), `AllowNegativeStock`, transfer Ship→InTransit→Receive, POS fitment year + supersession cross-ref. Migration `Phase3Inventory`.

### Degraded mode

- Web shows an **API unreachable** banner when the host cannot be contacted. Durable offline sale queue is not shipped yet (Phase 1.1).

## IIS notes

- API: ASP.NET Core module, no managed code, process path to published DLL
- Web: static site; rewrite unknown routes to `index.html`
- Terminate TLS at reverse proxy; forward `X-Forwarded-*` if needed
- Optional header `X-Company-Id` for company context override

## Ops checks

- `GET /health/live` → process alive
- `GET /health/ready` → SQL + outbox heartbeat (processor success within ~5 minutes; backlog threshold)
- `GET /health` → all registered checks
- Outbox processor runs as hosted service (FBR retries + GL events); updates readiness heartbeat each cycle
- Auto-backup hosted service respects CompanySettings intervals
- Rate limiter: 300 req/min per client on API
- JWT includes `company_id`, `branch_id`, and multi `branch_ids`; clients may send `X-Branch-Id` only if allowed

## Backup / restore

- Use Backup UI or `IBackupService` API
- Verify restore on a staging DB before production cutover
- Keep at least one off-box copy of nightly backups
- After restore: confirm `/health/ready`, open fiscal period, and sample GRN→AP→TB path

## Go-live checklist

- [ ] Default company / branches / warehouses linked
- [ ] Fiscal year open; current period open
- [ ] Number sequences INV/PO/JV/GRN/SO/DN/QT/PI/CC
- [ ] Account mappings present (SalesInvoice, Grn, PurchaseInvoice, Payment) including GRN Clearing 1400
- [ ] FBR sandbox credentials verified, then production token
- [ ] Roles reviewed (Admin/Manager/Sales/Inventory)
- [ ] Trial balance zero-proof after opening balances journal
- [ ] Change default **admin** password (forced on first login while still `admin123`)
- [ ] Smoke: health → login → password change → GRN post → AP match/post → POS checkout → TB/aging → FBR list/retry
- [ ] Confirm CORS includes the exact Web origin used by cashiers (LAN IP if applicable)

## Demo click-path (seeded)

1. Login `admin` / `admin123` (then change password)
2. Finance → Periods: confirm current month open
3. Inventory → GRN: create + post (warehouse + product line)
4. AP Invoices: create against GRN, Match, Post
5. Financial Reports → Trial Balance (export CSV optional)
6. POS: checkout with idempotency key; verify invoice + FBR stub
7. FBR page: submissions list + Retry if failed
