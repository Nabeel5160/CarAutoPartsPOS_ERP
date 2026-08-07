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
5. Start API once — `MigrateAsync` + seed create demo company, COA, fiscal periods, admin user. **Production:** `Seed:DemoData=false` (hard-blocked unless `CAP_ALLOW_DEMO_SEED=true`). See [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md).
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
- **Sandbox → production:** see [FBR-PRODUCTION.md](FBR-PRODUCTION.md) (token/NTN, metrics, outbox).
- **Cashiers must open a shift** before POS checkout (Settings/roles need `pos.shift`).
- Sales returns require a **reason code** and post GL via `SalesReturn` account mappings.
- **Phase 2:** Purchase requisitions → PO; GRN over-receive / QC hold / serials; 3-way match tolerances; supplier returns with stock+GL; reorder suggestions. Migration `Phase2Procurement` applies on API start.
- **Phase 3:** ATP (`/api/inventory/atp`), `AllowNegativeStock`, transfer Ship→InTransit→Receive, POS fitment year + supersession cross-ref. Migration `Phase3Inventory`.
- **Phase 4:** Opening balances → TB; period-close checklist; thin bank recon (1110); credit notes apply; AP tax GL split; `docs/COSTING.md`. Migration `Phase4Finance`. Cutover: post OB → verify TB → operate → close period with checklist.
- **Phase 5:** Inter-branch GIT (1350) + cost preserve; shift cash over/short (5200); warehouse BranchId + branch allow-list on transfers/POS; dashboard `?branchId=`. Migration `Phase5MultiBranch` + seeder EnsureAccount for 1350/5200 maps.
- **Phase 6:** MFA/TOTP for privileged users; approval matrix (PO/Transfer/AP/PeriodClose); money void-not-delete; immutable audit. Migration `Phase6Governance`. After deploy: restart API (migrate + seed policies/permissions), then enroll MFA for admin via `/mfa-setup`.
- **Phase 7:** Insights (dead/fast stock, GM%, avg/FIFO valuation, stock alert notifications); sales line UnitCost; AP void; audit filters. Migration `Phase7Insights`.
- **Phase 8:** First-run `/onboarding`; Cashier/Accountant role templates; `Seed:DemoData` (off in Production); EN/UR nav keys; `scripts/smoke-money-path.ps1` + GitHub CI. Migration `Phase8Packaging`.
- **Phase 9:** `UserBranch` ACL; JWT `branch_ids` from ACL (admin = all); TB/P&L `?branchId=` via cost center; Users branch assign UI. Migration `Phase9BranchAcl`.
- **Phase 10:** IndexedDB offline POS checkout queue (idempotent drain); multi-till + safe drops; shift requires till. Migration `Phase10CounterResilience`.
- **Phase 11:** Vertical profiles (`auto-parts` / `bike-parts` / `general-retail`); `AppConfigEntries` + Settings business profile; public branding API; module/field/behavior gates (FBR optional). See `docs/VERTICAL-PROFILES.md`. Migration `Phase11VerticalProfiles`. Optional seed env `CAP_VERTICAL`.
- **Phase 12:** Keyboard-first POS (F2/Enter/F4/F8/F9, picker arrows); OEM/SKU/barcode exact-match search indexes; receipt FBR IRN + reprint last; FBR metrics on dashboard/FBR page. See Stage 0 runbooks below. Migration `Phase12PosSearchIndexes`.

### Production seed (Phase 12.2)

- **`Seed:DemoData` must be `false` in Production** — already defaulted in `appsettings.Production.json`.
- Env override: `Seed__DemoData=false`.
- With demo off: platform still seeds company/COA/maps/till/admin; no demo products/extra users.
- Startup **mapping guard** throws if required Sales/GRN/AP/Payment maps or GL `1400` are missing after seed (`EnterprisePlatformSeeder`).

### FBR sandbox → production playbook (Phase 12.1)

Flip **without code change**:

1. **Checklist before prod**
   - [ ] Company NTN / STRN correct in Settings (seller identity)
   - [ ] POS Id matches FBR-registered POS
   - [ ] Production Bearer token from FBR DI portal (not sandbox token)
   - [ ] Sandbox posting success rate healthy on `/fbr` metrics (posted vs failed)
   - [ ] Ops can see Failed/Pending and use **Retry** (outbox)

2. **Config flip (API host)**
   - Set environment or `appsettings.Production.json`:
     - `Fbr__UseSandbox=false` (or `Fbr:UseSandbox: false`)
     - `Fbr__BearerToken=<production token>`
   - **Also** uncheck **FBR sandbox** in Settings / onboarding (`CompanySettings.FbrUseSandbox=false`) — runtime prefers company setting over appsettings for the DI URL.
   - Restart API (or recycle app pool). No redeploy of binaries required if using env vars.

3. **Verify**
   - One POS cash sale → receipt shows FBR IRN when posted
   - Dashboard **FBR success %** and **FBR needs retry** widgets
   - `GET /api/enterprise/fbr/metrics` and `/health/ready` (outbox heartbeat)

4. **Retry visibility**
   - FBR page lists Pending/Failed with **Retry** (enqueues `FbrSubmissionRequested` outbox)
   - Outbox processor retries automatically; readiness degrades on backlog

### Degraded mode

- Web shows an **API unreachable** banner when the host cannot be contacted.
- Cashiers can **queue POS checkouts** offline (IndexedDB); they sync automatically when the API returns (same `IdempotencyKey`). Not a multi-day offline-first store.
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

### Schedule (ops)

| Setting | Where | Default (seed) |
|---------|--------|----------------|
| Auto backup on/off | Web **Settings** → Company (`AutoBackupEnabled`) | On |
| Interval (hours) | Same form (`AutoBackupIntervalHours`) | 24 |
| Hosted poll | `BackupBackgroundService` (~15 min) | Creates SQL `.bak` when due |
| Manual | Web **/backup** or `POST /api/backups` | Anytime |

**File path:** `%LocalAppData%\CarAutoParts\Backups\` (`{DatabaseName}_{yyyyMMdd_HHmmss}.bak`). Keep at least one **off-box** copy of nightly backups.

### Restore-on-staging checklist

1. Take a fresh manual backup from production (or copy the latest successful `.bak`).
2. Restore onto a **staging** SQL instance via Backup UI (`/backup` → Restore from file) or `RESTORE DATABASE` — never restore over live without a freeze window.
3. Point staging API connection string at the restored DB; restart API.
4. Confirm `GET /health/ready` green; open fiscal period if required.
5. Smoke: login → GRN/AP sample or POS checkout → Trial Balance / aging glance.
6. Record date, operator, and result (pass/fail) in the pilot log.

### Restore drill cadence

- **Suggested:** every **D+7** during first pilots, then monthly in production.
- Ownership: site Admin / deployer named in the pilot sheet.
- Failure: if auto backup missing for > 1.5× interval, check Settings toggle, API host disk, and SQL `BACKUP` permissions; create a manual backup immediately.

### After any restore (prod or staging)

- Confirm `/health/ready`, open fiscal period, and sample GRN→AP→TB (or POS) path.

## Go-live checklist

- [ ] Default company / branches / warehouses linked
- [ ] Fiscal year open; current period open
- [ ] Number sequences INV/PO/JV/GRN/SO/DN/QT/PI/CC
- [ ] Account mappings present (SalesInvoice, Grn, PurchaseInvoice, Payment) including GRN Clearing 1400
- [ ] FBR sandbox credentials verified, then production token ([FBR-PRODUCTION.md](FBR-PRODUCTION.md))
- [ ] Roles reviewed (Admin/Manager/Sales/Inventory)
- [ ] Trial balance zero-proof after opening balances journal
- [ ] Change default **admin** password (forced on first login while still `admin123`)
- [ ] Smoke: health → login → password change → GRN post → AP match/post → POS checkout → TB/aging → FBR list/retry
- [ ] Confirm CORS includes the exact Web origin used by cashiers (LAN IP if applicable)
- [ ] Confirm `Seed:DemoData=false` in Production (no demo users/products)
- [ ] Pilot path: [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md)

## Demo click-path (seeded)

Demo users (when `Seed:DemoData=true`):

| User | Password | Role |
|------|----------|------|
| admin | admin123 | Admin (force password change) |
| manager | manager123 | Manager |
| sales | sales123 | SalesUser |
| cashier | cashier123 | Cashier |
| inventory | inventory123 | InventoryUser |
| accountant | accountant123 | Accountant |

1. Login `admin` / `admin123` (then change password)
2. Complete `/onboarding` if prompted (fresh DBs)
3. Periods → GRN post → AP match/post → TB → POS → FBR retry
4. Optional API smoke: `pwsh ./scripts/smoke-money-path.ps1`
2. Finance → Periods: confirm current month open
3. Inventory → GRN: create + post (warehouse + product line)
4. AP Invoices: create against GRN, Match, Post
5. Financial Reports → Trial Balance (export CSV optional)
6. POS: checkout with idempotency key; verify invoice + FBR stub
7. FBR page: submissions list + Retry if failed
