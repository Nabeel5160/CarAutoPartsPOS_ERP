# Enterprise Mid-Market Hardening — Changelog

## 2026-08-03 — QA loop: Program A/B/C1 smoke, regression & API coverage

QA + fix pass over Light CRM (A), Ops gaps (B), and Service Light (C1). No feature code was found broken; work was additive test coverage plus one new test project. See [QA-PROGRAM-ABC.md](QA-PROGRAM-ABC.md) for the full report.

### Added
- `tests/CarAutoParts.Api.Tests` (new project, `Microsoft.AspNetCore.Mvc.Testing` + InMemory EF Core): first real HTTP-pipeline test host for this repo (`ApiTestFactory : WebApplicationFactory<Program>`), exercising routing, JWT auth, permission policies, and `RequireFeature` module gates end-to-end.
  - `CrmApiTests` — smoke endpoint (ok/unauthorized/forbidden), lead create+list, convert-to-customer idempotency, pipeline dashboard weighted revenue, lost-without-reason 400, module-disabled 404.
  - `ServiceApiTests` — smoke endpoint, ticket create+list, company-filtered list, resolve-requires-notes, closed-ticket-cannot-transition.
- `tests/CarAutoParts.Application.Tests/ProgramBOpsGapsTests.cs` — Program B regression the API layer doesn't expose directly: RFQ create/send/vendor-quote/select/create-PO lifecycle, sales target CRUD + duplicate-period + validation, supplier payment withholding-tax GL lines + field persistence.

### Verified (no changes needed)
- CRM (`CrmFoundationW0Tests`), Service tickets (`ServiceTicketTests`), and the `GET /api/crm/smoke` / `GET /api/service/smoke` endpoints all behave as documented.
- Program B UI/API surfaces (RFQ, sales targets, cash flow, bank reconciliation, WHT fields) are present and wired; `BackupsController` "placeholder" was already removed per [MASTER-ROADMAP.md](MASTER-ROADMAP.md).

### Known pre-existing gaps (not fixed in this pass — out of scope / date-sensitive fixtures)
- 11 pre-existing `CarAutoParts.Application.Tests` failures across `Phase2ProcurementTests`, `Phase3InventoryTests`, `Phase4FinanceTests`, `Phase5MultiBranchTests`, `Phase6GovernanceTests`, and `DocumentPostingIntegrationTests` — all `System.InvalidOperationException: No open accounting period for document date.` from fixed calendar-date test fixtures vs. the real system clock. Unrelated to Programs A/B/C1.

## 2026-08-03 — Program C1: Service Light + Mobile (thin slice)

Program C (multi-quarter remainder) starts with **C1**. See [MASTER-ROADMAP.md](MASTER-ROADMAP.md) Phase 8 / Phase 11 for scope and what's still PARTIAL/TODO.

### Domain / Data
- `ServiceTicket` (`CompanyEntity`): customer link, subject/description, `ServiceTicketStatus` (Open/InProgress/Resolved/Closed), `ServiceTicketPriority` (Low/Normal/High/Urgent), optional warranty flag + warranty/AMC free-text reference, optional product link, assigned user, open/due/resolved/closed dates, notes + resolution notes
- Migration `20260803160000_ProgramC1ServiceLight`; module key `service.tickets` added to vertical profiles (enabled by default across auto-parts/bike-parts/retail)
- Permissions `service.view` / `service.manage`; seeded for Admin, Manager, SalesUser roles

### API
- `ServiceController` (`/api/service`, `/api/v1/service`) — `RequireFeature(service.tickets)` + `Authorize`: list (company/status/priority/customer filtered, paged), get by id, create, update, status-change (with resolution notes on Resolve/Close), and per-customer ticket list for Customer 360

### Web
- `/service/tickets` — list with status/priority/customer filters + create form (customer/product pickers, not raw IDs)
- `/service/tickets/{id}` — detail with field edits and status-change workflow
- Nav: new **Service** group ("Tickets") gated by `service.view` + module flag
- Customer 360 (`/crm/customers/{id}`): "Service tickets" card showing that customer's tickets, link to full list

### Mobile
- `/m/service` — mobile ticket list (status/priority filter) + quick status update / resolve with notes; tile added to `MobileHub` gated by `service.view`
- `/m/stock` camera barcode scan: "Scan barcode" button using the browser `BarcodeDetector` API (`barcode-scanner.js`) to fill SKU search — graceful no-op on unsupported browsers (no iOS Safari support yet)

### Tests
- `ServiceTicketTests` (Application layer): create ticket, company-scoped listing, status transition, resolution-notes requirement, vertical-profile module inclusion

### Honest scope note
This is a **thin ticket workflow**, not a field-service/SLA suite: no SLA timers, no knowledge base, no service customer portal, no technician scheduling, and warranty/AMC are free-text references (not dedicated claim/contract entities). Extended (multi-day) offline was **not** attempted in this pass — see [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md).

## 2026-08-02 — CRM W0 foundation

- Light CRM foundation: `Lead`, `CrmActivity`, `Opportunity` (`CompanyEntity`); migration `CrmFoundationW0`
- Permissions `crm.view` / `crm.manage` / `crm.leads` / `crm.activities`; module toggle `sales.crm`
- API `CrmController` (`RequireFeature`) + Web CRM nav (Leads / Tasks / Pipeline)
- Loop doc: [CRM-LOOP.md](CRM-LOOP.md)

## 2026-08-02 — Analytics Graphs tab (4D charts)

### Web UI (`Analytics.razor` + `CapViewToggle`)
- Filters + Export stay above tabs; toggle order **Numbers | Graphs** (default Numbers)
- **Numbers**: KPI cards + list cards only (empty-state hints)
- **Graphs**: chart-focused — top-seller CapChartPlayer race, ABC doughnut, dead/fast bars, slow/dead bubble (grouped-bar fallback), GM vs inventory, **4D stock insight** CapChartPlayer timeline (progressive Top N / ABC / dead / fast frames from existing `AnalyticsDto`)

## 2026-08-02 — Shell sidebar layout & fullscreen

### Web UI (`MainLayout` + `cap-theme.css` + `shell.js`)
- Topbar **Sidebar** toggle: expanded (default) vs boxed/compact icon rail (`cap-shell--sidebar-boxed`); preference persisted as `cap.shell.sidebarBoxed`
- Desktop-only boxed layout with hover/focus expand overlay; mobile Menu drawer unchanged
- Topbar **Full** / **Exit** fullscreen via Fullscreen API (`.cap-scene`); Escape/browser exit syncs button state (`cap-shell--fullscreen`)

## 2026-08-01 — Shop logo & identity

### Settings / API
- `POST /api/settings/logo` (multipart png/jpg/jpeg/webp, max 2MB) and `DELETE /api/settings/logo` — requires `settings.manage`
- Files stored at API `wwwroot/uploads/company/logo.{ext}`; served via `UseStaticFiles`
- **LogoUrl** (canonical) kept in sync with **LogoPath**; relative URL shape: `/uploads/company/logo.png` (resolve against API base, e.g. `http://host:5280/uploads/...`)
- Settings UI: preview, upload/replace, remove; Invoice footer field; branding Logo URL read-only when from upload (advanced edit optional)

### Branding surfaces
- Sidebar (`.cap-brand`), login, and `capBrand.apply` show logo `<img>` when set
- POS receipt header: logo + company name/address/city/phone/NTN; footer uses `InvoiceFooter` or “Thank you”

## 2026-08-01 — Graphs / Numbers view toggle

### Web UI
- `CapViewToggle` + `CapViewMode` — Numbers | Graphs segmented control (`btn-cap` / `btn-outline-light`)
- **Dashboard** defaults to Graphs (charts); Numbers shows KPI cards only
- **Reports** / **Analytics** / **Financial P&amp;L** default to Numbers (tables/lists); Graphs shows chart overlays
- Excel/PDF/CSV export controls remain available in both modes

## 2026-08-01 — 4D animated graphs (Apache ECharts)

### Charts
- Replaced Chart.js path with Apache ECharts via `capCharts.js` (lazy CDN load; `echarts-gl` only for bar3D)
- `CapChart` / `CapChartPlayer` / `CapChartFactory` — timeline scrub, PNG export, theme retheme, reduced-motion safe
- Dashboard: series DTOs fixed; pulse timeline + executive bar3D; `GET /api/dashboard/timeline`
- Reports overlays (daily-sales, sales-dim/staff, profit-dim, tax, stock-aging); sales-dim click→table filter
- Analytics + Financial P&amp;L light charts; [ANIMATED-CHARTS.md](ANIMATED-CHARTS.md)

## 2026-08-01 — Counter UX polish loop W1–W5

### POS (`Pos.razor` + `cap-theme.css`)
- **W1** — Last till in localStorage; auto-open shift when one till; `EmptyState` / open-shift CTA; offline banner copy; `cap-pos-shift-bar` compact ≤768px
- **W2** — 140ms search debounce; warm-catalog prefix filter; `cap-pos-exact` + supersession badge; cart ± qty
- **W3** — Cash tendered / change due; recall confirm; buyer/FBR in `<details>`; F9 busy toast
- **W4** — FBR pending vs failed + Open FBR; offline queue item errors; recent receipts (last 5) reprint menu; Retry print
- **W5** — Density + high-contrast toggles (persisted); search/cart/result motions; `--cap-*` POS styles

### Docs
- [COUNTER-UX-POLISH-LOOP.md](COUNTER-UX-POLISH-LOOP.md) W1–W5 marked **Done**

## 2026-08-01 — Counter UX polish loop W0

### Auth / MFA (counter path)
- `MfaEnrollmentPolicy`: cashiers with `pos.checkout` who lack `users.manage` / `finance.manage` / `platform.manage` are **not** forced to MFA enroll unless `MfaEnforced`
- Privileged admin/finance still get `MustEnrollMfa`; MFA setup **Skip** kept
- `Login.razor` / `MfaSetup.razor`: counter path → `/pos`; admin force-password → settings

### Seed
- Demo ACL query uses `List<string>` for EF `Contains` (avoids `ReadOnlySpan`1[System.String]` expression-tree failure on array)
- Demo users explicitly `MustChangePassword = false`; seeder clears that flag on existing demo non-admins; admin/`admin123` still forced

### POS
- Catalog warm starts in parallel on `/pos` init; search focus unchanged; debounce kept
- Print after sale is fire-and-forget; sale success toast separate from print warn; **Reprint last** chip in shortcuts + cart panel

### Tests / docs
- `MfaEnrollmentPolicyTests` + Auth login mustEnroll cases
- [COUNTER-UX-POLISH-LOOP.md](COUNTER-UX-POLISH-LOOP.md) (W0 Done, W1–W5 planned); linked from docs index

## 2026-07-31 — Grid pagination (W0–W5)

### Shared
- `Components/Pager.razor` + `.cap-pager` theme styles (25/50/100, Prev/Next, page indicator)
- CapApi `GetInventoryAsync` / `GetMovementsAsync` accept `pageSize`

### API / Application
- Notifications: `PagedResult` via `QuerySpec` (removed silent `Take(100)`)
- Enterprise lists paged: quotations, wholesale sales orders, deliveries, GRN, AP invoices, price lists, FBR submissions, kits
- Purchase requisitions, brands/warehouses/users optional paged GET when `page`/`pageSize` present

### Web pages with Pager
Products, Inventory, Movements, Customers, Suppliers, Audit, Notifications, Serials, Transfers, Invoices, Returns, Quotations, Sales orders, Deliveries, Purchases, GRN, AP invoices, Requisitions, Journals, Brands, Warehouses, Users, Kits, Price lists, FBR, Mobile `/m/stock`

### Deferred
Periods, COA, opening balances, bank recon, account mappings, Approvals inbox, Categories tree, POS holds, reports/analytics

### Tests
- `NotificationPagingTests`; Phase14 wholesale list uses `PagedResult.Items`

### Docs
- [PAGINATION-PLAN.md](PAGINATION-PLAN.md) waves marked Done

## 2026-07-31 — Phase 17 Mobile Light (Stage 1, optional late)

### Responsive shell + login
- Drawer backdrop, close-on-nav, touch-sized Menu/Logout; accent picker hidden on xs
- Topbar **Mobile** chip (phone) → `/m`; denser padding under ~992px
- Login: larger controls + safe-area padding on ~390px viewports

### Dedicated mobile routes (card UI, not native app)
- `/m` — hub (stock check + approvals tiles; permission/module gated)
- `/m/stock` — read-only SKU/name search + low-stock list (`inventory.view` + `inventory.stock`)
- `/m/approvals` — pending inbox with large Approve/Reject (`approvals.view` / `approvals.decide`)

### Desktop pages usable on phone
- `/inventory` — search box; card list under `md`; hide adjust/receive/overstock chrome on phone
- `/approvals` — card pending list under `md`; policies table desktop-only; link to phone view

### Nav / CSS
- System → **Mobile** → `m` (exact); `ModuleForRoute` maps `m/stock` → `inventory.stock`
- `cap-theme.css`: `.cap-m-*` components using Phase 18 `--cap-*` tokens

### Verify (DevTools ~390px or phone)
1. `/login` — sign-in fields and button fit without horizontal scroll
2. Open Menu → navigate; backdrop closes drawer
3. `/m` → Stock check: search returns cards; Low stock list readable
4. `/m/approvals` (or `/approvals` on phone): Approve/Reject tappable
5. `/inventory` on phone: cards + search; no adjust/receive forms

### Deferred
- Full PWA install / service worker; branch-scoped mobile stock (Q2 P1); native apps; POS mobile redesign

### Schema / Application
- No new migration; no Application-layer changes (reuse existing inventory + approvals APIs)

## 2026-07-31 — Phase 18 Design System (Stage 2)

### Tokens & shell
- Expanded `wwwroot/css/cap-theme.css` with shared `--cap-*` design tokens: color (existing brand), spacing, type scale, radii, borders, focus ring, semantic danger/success
- Shell (MainLayout topbar/sidebar/content), login panel, buttons (`btn-cap` / ghost / outline), tables, forms, and `alert-cap` consume tokens
- Coexists with Phase 17 mobile CSS — mobile classes use the same variable system (no forked theme)

### Shared patterns (exemplars)
- `PageHeader` → `.cap-page-header` / `.cap-page-header-actions`
- New `EmptyState` component → `.cap-empty` / title / message / actions
- Applied on **Dashboard**, **Products**, **Settings** (section titles + empty states); other pages keep existing markup and inherit tokenized shell/controls

### Docs
- Roadmap Phase 18 Q2 P0 marked Done (pragmatic); guided tour / full surface polish deferred to Q3–Q4 Stage 2 backlog

## 2026-07-31 — Phase 19 Performance Budgets (Stage 2)

### Docs
- [PERFORMANCE.md](PERFORMANCE.md) — POS search / checkout / day-sales / dashboard latency budgets + guardrail table
- Smoke timings documented; linked from docs index + roadmap

### Query shaping
- `QueryLimits` + `ReportDateRange` — max page size 500; interactive reports ≤93 days; exports ≤366 days
- `GetDailySalesSummaryAsync` rewritten to `AsNoTracking` + server-side aggregates (no full invoice/payment materialization)
- Sales returns report: date-range clamp + `Take` cap + `AsNoTracking`
- POS search Take caps via `QueryLimits`; supersession lookups `AsNoTracking`
- Dashboard invoice/PO/inventory reads: `AsNoTracking`

### Indexes
- Migration `Phase19ReportAndPosIndexes`: `SalesInvoices(InvoiceDate, WarehouseId)`, `SalesReturns(ReturnDate)`, `ProductVehicleCompatibilities(Make, Model)`

### Web / smoke
- Reports page: skip duplicate fetch when default `?type=` URL sync fires after first load
- `scripts/smoke-money-path.ps1` prints elapsed ms for POS products + daily-sales

### Tests
- `ReportDateRangeTests`; `ClientReportsTests` range rejection

## 2026-07-31 — Phase 18 Design System (Stage 2)

### Tokens & shell
- Expanded `wwwroot/css/cap-theme.css` with shared `--cap-*` design tokens: color (existing brand), spacing, type scale, radii, borders, focus ring, semantic danger/success
- Shell (MainLayout topbar/sidebar/content), login panel, buttons (`btn-cap` / ghost / outline), tables, forms, and `alert-cap` consume tokens
- Coexists with Phase 17 mobile CSS — mobile classes use the same variable system (no forked theme)

### Shared patterns (exemplars)
- `PageHeader` → `.cap-page-header` / `.cap-page-header-actions`
- New `EmptyState` component → `.cap-empty` / title / message / actions
- Applied on **Dashboard**, **Products**, **Settings** (section titles + empty states); other pages keep existing markup and inherit tokenized shell/controls

### Docs
- Roadmap Phase 18 Q2 P0 marked Done (pragmatic); guided tour / full surface polish deferred to Q3–Q4 Stage 2 backlog

## 2026-07-31 — Phase 16 Report Cadence (Stage 1 / Q4 P1)

### PDF branch ACL parity
- Shared `ReportBranchScope` used by Excel (`ReportService`) and PDF (`PdfReportService`) — same `branch_ids` / warehouse scoping
- Inventory / sales / purchases PDFs deny disallowed `branchId`; soft-delete / void filters aligned with Excel
- Daily sales PDF already used ACL-filtered `ReportService` aggregates (unchanged)

### Z archive Excel
- Optional `GET /api/reports/z-shifts?format=xlsx` via `ExportClosedShiftsArchive` (list already ACL-filtered by `PosFloorService`)
- Reports UI **Export XLSX** on Z shifts tab downloads archive workbook

### Deferred (roadmap later quarters)
- Scheduled email report packs — Q1 2027 P1
- Manager PDF week pack — Q2 2027 P2

### Tests / docs
- `Phase16ReportCadenceTests` — shared `ReportBranchScope` ACL + Z archive Excel + PDF deny
- [CLIENT-REPORTS-ROADMAP.md](CLIENT-REPORTS-ROADMAP.md) + [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) updated

## 2026-07-31 — Phase 15 Warehouse Locations (Stage 1)

### P0 — Bin / location master + balance dimension
- `WarehouseLocation` master per warehouse (code, name, receiving/pick defaults, active)
- `InventoryLocationBalance` tracks Product×Warehouse×Bin qty; warehouse `InventoryItem` remains ATP rollup
- **ATP policy:** available-to-promise stays warehouse-level (`QuantityOnHand − ReservedQuantity`). Location balances are putaway/pick dimensions kept in sync; POS/reservations do not require a bin
- Migration `20260731120000_Phase15WarehouseLocations` seeds `MAIN` bin per warehouse and backfills balances from on-hand

### P0 — Receive / putaway assigns bin
- GRN line optional `WarehouseLocationId`; post putaways to that bin (or receiving-default / auto-`MAIN`)
- Web GRN create accepts Putaway Loc Id

### P0 — Cycle count by bin
- Cycle count header/lines optional `WarehouseLocationId`; empty lines seed from location balances when bin scoped
- Variance posts adjust both warehouse rollup and bin balance
- Web count sheet shows bin + system/counted/variance

### P1 — Pick list before ship
- Transfer / delivery lines: `FromLocationId` / `ToLocationId`, `IsPicked`
- `POST /api/transfers/{id}/confirm-pick` and `POST /api/enterprise/deliveries/{id}/confirm-pick` required before ship
- Ship moves stock from pick bin; transfer receive putaways to destination bin

### API / Application
- `GET/POST/PUT/DELETE /api/warehouses/{id}/locations` · `GET .../locations/balances`
- Application tests: `Phase15WarehouseLocationsTests`

### Schema
- Migration `Phase15WarehouseLocations` (`20260731120000_Phase15WarehouseLocations`)

## 2026-07-31 — Phase 14 Wholesale Loop (Stage 1)

### P1 — Quote → SO → Delivery → Invoice UI
- End-to-end happy path on Web: Quotations → Sales Orders → Deliveries → Invoices (no API tools required)
- Convert quotation → SO with **credit limit enforcement** and clear error text (limit / balance / available)
- SO actions: **Create delivery** (lines copied from SO) · **Create invoice**
- Delivery actions: **Ship** · **Create invoice** (after ship)
- Document chain visible: quote # → SO # → delivery # → invoice # (cross-links between pages)
- Phase 11 module gates on wholesale enterprise endpoints (`sales.quotations` / `sales.orders` / `sales.deliveries` / `sales.invoices`)
- Permissions respected (`quotations.manage`, `deliveries.manage`, `sales.view`)

### P2 — Price list on quote/SO lines
- Create quote resolves price list / catalog when unit price is 0
- Line UI shows price source (`PriceList` / `Catalog` / `Override`) and list name on quotations and sales orders
- Override gated by new `sales.price.override` (also accepts existing `pos.price.override`)

### API / Application
- `GET /api/enterprise/sales-orders` (chain-enriched wholesale list + line price source)
- `POST .../sales-orders/{id}/create-delivery` (rejects duplicate open DN)
- `POST .../sales-orders/{id}/create-invoice` (credit check; stock issue when no prior ship)
- `POST .../deliveries/{id}/create-invoice`
- Quote→SO link via Notes marker `[SourceQuote:{id}:{number}]` (no schema migration)
- Application tests: `Phase14WholesaleLoopTests`

### Schema
- No new migration (reuses existing SalesQuotations / SalesOrders / DeliveryNotes / SalesInvoices / PriceLists)

## 2026-07-31 — Phase 13 Catalog Depth (Stage 1)

### Fitment UX (P0)
- POS make/model/year picker when `pos.fitmentSearch` enabled; fitment summary on product cards
- Products editor: fitment rows when `product.fitment` visible; fitment filter on list
- Supersession display (supersedes / superseded-by) on POS cards/picker and product detail when `pos.supersession` enabled
- Gates honored — fitment/supersession UI and search enrichment hide when vertical disables them

### Barcode + OEM search (P0)
- Scanner paste path: trim + leading-zero candidate expansion; exact barcode/SKU/OEM/part before fuzzy Contains
- Multi-match picker (↑↓/Enter) retained; unique exact barcode/SKU/OEM adds to cart without extra click
- `GET /api/pos/fitment-options`; POS products accept `make` / `model` / `year` query params

### Bulk OEM/fitment CSV (P1)
- `POST /api/products/import-oem-fitment` (permission `products.import`)
- CSV columns: `Sku,OemNumber,PartNumber,Make,Model,YearFrom,YearTo` — upsert OEM/part + add fitment rows; does not wipe catalog
- Error report CSV downloadable from Products UI when bad rows present
- Application tests: `Phase13CatalogDepthTests`

### Schema
- No new migration (uses existing Products / ProductVehicleCompatibilities / ProductSupersessions)

## 2026-07-31 — Stage 0 / Phase 12 + 12.1 + 12.2 (counter polish, FBR, pilot)

### Phase 12 — Counter polish
- POS keyboard: F2 search, Enter add, Esc clear, F4 hold, F8 qty, F9 pay; ↑↓ picker; Shortcuts strip; focus returns to search after add
- Debounced product search (~180ms) with in-flight cancel; **exact SKU/barcode/OEM/part hot path** (indexed equality before Contains/fitment)
- Migration `Phase12PosSearchIndexes` — OEM/part indexes
- Receipt print: auto-print after checkout; FBR IRN on receipt when posted; clear warn + retry; reprint last; offline drain success/fail UX
- Empty cart / held-sale clarity (P2 light)

### Phase 12.1 — FBR production hardening
- Docs: [FBR-PRODUCTION.md](FBR-PRODUCTION.md) + DEPLOYMENT sandbox→prod playbook (token/NTN, config flip)
- `GET /api/enterprise/fbr/metrics`; Dashboard + `/fbr` success-rate / needs-retry widgets
- `CompanySettings.FbrUseSandbox` overrides appsettings URL; Pending/Failed **Retry** visible
- Checkout: FBR post/persist exceptions cannot fail the committed sale; still enqueue outbox retry

### Phase 12.2 — Pilot packaging
- Production hard-blocks `Seed:DemoData` unless `CAP_ALLOW_DEMO_SEED=true`; `Seed:DemoData=false` in Production
- Onboarding creates **TILL-01** when missing; post-finish POS/FBR hints
- [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md) for first 5 pilots (ACL, roles, backup, health)

## 2026-07-31 — Top-tier niche roadmap (docs)

- Added [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md): Stages 0–5 climb plan, Q backlog for Stages 0–2, Phase **12+** epic names, KPIs, out-of-scope-early, code-area mapping
- Linked from [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md)
- Docs only — no product behavior change

## 2026-07-30 — Client Reports Phases A–C

- Daily sales, Z archive, X-report, sales returns; branch ACL on operational Excel exports
- Sales dim/staff, profit dim, movements, PO/GRN pipeline, AR/AP aging Excel, analytics Excel
- Tax/GST, FBR register (module-gated), stock aging (best-effort), SKU margin; PDF wired via QuestPDF
- See [CLIENT-REPORTS-ROADMAP.md](CLIENT-REPORTS-ROADMAP.md)

## 2026-07-30 — Phase 11 configurable vertical profiles

### Business profile
- Vertical presets: `auto-parts`, `bike-parts`, `general-retail` (default remains auto-parts)
- `AppConfigEntries` table + `CompanySettings.VerticalKey` / `LogoUrl`
- Modules / fields / behaviors / brand / label overrides editable in Settings UI
- Anonymous `GET /api/app-config/public` for login branding; full config behind settings permissions

### UI / POS
- Nav gated by module + permission; disabled routes redirect home
- Product editor shows OEM / part / HS when fields enabled
- POS search placeholder and OEM subline follow config; FBR posting skipped when `behavior.fbr.enabled=false`

### Seeding
- Categories/brands from `VerticalSeedPacks`; demo products only for auto-parts
- Env `CAP_VERTICAL` selects pack on fresh seed

### Migration
- `20260730210000_Phase11VerticalProfiles`

## 2026-07-28 — Phase 10 counter resilience

### Offline POS queue
- IndexedDB outbox (`wwwroot/js/offline-outbox.js`) queues checkout payloads with client `IdempotencyKey`
- POS **Queue sale** when API down; MainLayout **Queue N** chip; auto-drain on reconnect (idempotent server dedupe)
- Shift close **hard-blocked** while pending/failed queue items remain
- Max 100 items / 24h age policy

### Multi-till + safe drops
- `Till` per branch (seeded `TILL-01`); `CashierShift.TillId` required on open; one open shift per till
- Safe drops during open shift reduce Z-report / expected cash
- APIs: `GET/POST /api/pos/tills`, `POST/GET /api/pos/shifts/{id}/safe-drops`
- Branch ACL enforced on till open

### Migration
- `20260728200000_Phase10CounterResilience`

## 2026-07-28 — Phase 9 multi-branch ACL & branch P&L

### User ↔ branch ACL
- `UserBranch` (UserId, BranchId, IsDefault); unique per user+branch
- Admin / `platform.manage` → all company branches in JWT; others from ACL (no rows → company default only, not all)
- JWT `branch_id` + multi `branch_ids`; `X-Branch-Id` rejected if outside allow-list
- Users API/UI assign branches + default; demo/non-admin users seeded to default branch

### Branch P&L / TB
- `GET .../reports/trial-balance?branchId=` and `profit-loss?branchId=` filter posted lines by `CostCenter.BranchId`
- Disallowed branch → 400; Financial Reports page branch selector

### ABAC
- Transfer create checks source **and** destination warehouse branches; POS/dashboard/warehouse already enforced

### Migration
- `20260728190000_Phase9BranchAcl`

## 2026-07-28 — Phase 8 packaging

### Onboarding
- `CompanySettings.SetupCompletedAt`; wizard at `/onboarding` + `GET/POST /api/onboarding/*`
- Web gate redirects until setup complete (existing DBs backfilled complete)

### Role templates
- Added **Cashier** (POS, no price override) and **Accountant** (finance/reports, no POS)
- Startup syncs all templates (not Admin-only); Users page “Apply template” dropdown
- Demo users: `cashier` / `cashier123`, `inventory` / `inventory123`, `accountant` / `accountant123`

### Demo seed gate
- `Seed:DemoData` (default true; **false** in Production `appsettings.Production.json`)

### EN/UR
- Expanded LocaleService keys for nav, login, finance, POS, onboarding (~40 keys)

### Smoke / CI
- `scripts/smoke-money-path.ps1`; GitHub Actions `.github/workflows/ci.yml`
- `Phase8PackagingTests`

### Migration
- `20260728180000_Phase8Packaging`

## 2026-07-28 — Phase 7 insights (+ 6.1 close-out)

### Phase 6.1 close-out
- Cashier shift DTO exposes `VarianceJournalEntryId`
- Audit: search/filter by `Action` (e.g. Void), entity type, dates; richer Audit UI
- Void purchase invoices (`POST /api/approvals/void/purchase-invoices/{id}`) with reversing AP GL; Journals UI Void action

### Insights hub
- Analytics: dead stock, fast movers, GM$ / GM%, optional `branchId` + `deadStockDays`
- Web Insights page shows profit, dead/fast, margin KPIs

### Margin / COGS
- `SalesInvoiceLine.UnitCost` stamped from inventory deduct (avg/FIFO)
- Profit export includes COGS / profit / GM%; voided sales excluded from analytics

### Valuation
- `GET /api/inventory/value?method=Average|Fifo` with warehouse/branch filters; Inventory UI toggle

### Alerts
- Receive/deduct/adjust creates deduped LowStock / Overstock notifications

### Migration
- `20260728160000_Phase7Insights`

## 2026-07-28 — Phase 6 governance

### MFA / TOTP
- Privileged users (finance / users.manage / platform.manage) prompted to enroll; enabled MFA requires code after password
- APIs: `POST /api/auth/mfa/verify`, enroll begin/confirm, disable, admin reset
- Web: Login MFA step + `/mfa-setup`

### Approval matrix
- `ApprovalPolicy` / `ApprovalRequest`; default seeds for large PO / Transfer / AP / PeriodClose
- Gates PO approve, Transfer approve, AP post, period close; inbox at `/approvals`

### Audit & void
- Audit actions: Post, Void, Approve, Reject; audit logs immutable
- Void journals (reversing entry) and sales invoices via `/api/approvals/void/...`; block delete of posted money docs

### Migration
- `20260728140000_Phase6Governance`

## 2026-07-24 — Phase 5 multi-branch

### Inter-branch transfers
- Ship/receive posts GIT GL via COA **1350** (`InventoryTransfer` / GoodsInTransit + Inventory)
- `ShippedUnitCost` preserves cost into destination warehouse; movements use `StockMovementType.Transfer`
- Same-branch transfers: stock only (no GIT GL); Transfers UI shows Inter-branch badge

### Cashier shift variance
- Close shift captures declared cash vs Z expected → `CashVariance`; over/short GL via **5200** (`CashierShift` OverShort + Cash)
- POS close uses declared cash input

### Warehouse / ABAC
- Warehouse `BranchId` required; transfer create + POS open/checkout reject disallowed branches

### Dashboard
- `GET /api/dashboard?branchId=` filters KPIs by branch warehouses; Web branch selector + cash variance / open shifts

### Migration
- `20260728120000_Phase5MultiBranch`

## 2026-07-28 — Phase 4 finance cutover & control

### Opening balances
- Guided pack: inventory + AR/AP (+ optional GL) with equity 3100 offsets; sets `OpeningBalanceDate`
- GL posts before cutover blocked except `OpeningBalance` docs
- Blazor: Opening Balances under Finance

### Period-close checklist
- Checklist before close: draft journals / TB imbalance block; GRN/AP/FBR/bank warn
- Force-close requires `finance.force-close`

### Bank reconciliation (thin)
- Statements + lines vs GL **1110**; match/clear; recon report
- Blazor: Bank Recon

### Credit notes
- Sales/purchase returns get `CN-` / `SCN-` numbers, line tax, `StockAffected`, apply-to-invoice

### Tax & costing
- AP purchase invoice posts Tax line separately; POS uses `DefaultTaxRate` when product tax is 0
- `docs/COSTING.md`; `DefaultValuationMethod` for new inventory items

### Migration
- `20260728090000_Phase4Finance`

## 2026-07-27 — Phase 3 inventory discipline

### ATP
- `IAtpService`: available = on-hand − reserved; `GET /api/inventory/atp`
- POS checkout stock gate uses ATP (skipped when `AllowNegativeStock`)

### Negative stock policy
- Company setting `AllowNegativeStock` (default false); `DeductStockAsync` respects it

### Transfers
- Explicit **Ship** (Approved → InTransit, deduct source) then **Receive/Complete** (InTransit → Completed, receive dest)
- Completing an Approved transfer still ships then receives for compatibility
- Web Transfers: Approve / Ship / Receive actions

### POS fitment / cross-ref
- Search includes vehicle year range on fitment
- Supersession: searching an old SKU/OEM also returns the replacement product

### Migration
- `20260727030000_Phase3Inventory`

## 2026-07-27 — Phase 2 procurement depth

### Purchase requisitions
- `PurchaseRequisition` lifecycle: Draft → Submitted → Approved/Rejected → Converted
- Approve uses `purchases.approve`; convert creates Draft PO; permission `purchases.requisition`
- Blazor: Requisitions + Reorder pages under Purchasing

### GRN receive rules
- Company settings: `GrnOverReceivePercent` (default 0), `GrnUnderReceiveAllowed`
- Optional landed cost lines (Freight/Duty/Other) summed into `LandedCostAmount`
- Serial capture required when product `TrackSerialNumbers`; registered on post
- Soft QC: post can leave `QcHold`; AP 3-way match blocked until Release QC

### Three-way match
- `ThreeWayQtyTolerancePercent` / `ThreeWayPriceTolerancePercent` on CompanySettings
- Rules updated in `ThreeWayMatchRules` + `MatchThreeWayAsync`

### Supplier returns + reorder
- Purchase return requires reason code; deducts stock; reduces supplier balance; GL `PurchaseReturn` maps
- Returns page: Sales + Purchase tabs
- Reorder suggestions from Min/Reorder/MaximumStock → draft PR (no velocity — Phase 2.1)
- Product `MaximumStock` + PO `SupplierBackorderNotes`

### Migration
- `20260726202735_Phase2Procurement`

## 2026-07-27 — Phase 1 shop-floor

### POS floor
- Multi-tender checkout with change due / credit (AR) + credit-limit check
- Price list resolution + `pos.price.override` gate; kit component explode on sale
- POS search: OEM, part number, vehicle make/model
- Held sales (park/recall/discard); cashier shift open/close + Z-report
- Checkout requires open shift when authenticated; receipt HTML print endpoint
- Web POS: shift strip, hold/recall, tenders, ambiguous match picker, print

### Returns
- Reason codes required; invoice qty remaining validation; transactional GL (`SalesReturn` maps seeded)

### Ops / UX
- API unreachable banner (`ApiReachability`); queue intent deferred to Phase 1.1
- Permissions: `pos.price.override`, `pos.hold`, `pos.shift`

## 2026-07-27 — Phase 0 baseline harden

### Auth / LAN
- `AppUser.MustChangePassword`; login/`/me` expose flag; change-password clears it
- Seeded admin starts with force-change; existing admin still on `admin123` is re-flagged on startup
- Blazor redirects to Settings until password changed; 401 clears token and sends user to login
- ProblemDetails `detail`/`title` surfaced in Web toasts

### POS / money path
- `IUnitOfWork.ExecuteInTransactionAsync` wraps invoice + stock + payment + GL
- FBR runs after commit; failure keeps sale and enqueues outbox retry (documented)
- POS UX: search focus, Enter scan-add, qty edit, tender Cash/Card/Bank/Credit, F2/F9

### Seed / docs
- Enterprise seeder throws if required SalesInvoice/Grn/PurchaseInvoice/Payment maps or GL `1400` missing
- `docs/PRODUCT-POSITIONING.md`; DEPLOYMENT updated for password force, CORS/LAN, FBR non-rollback

## 2026-07-24

### Posting pipeline
- `IAccountingPeriodService.EnsureOpenAsync` gates document GL posts
- `IGlPostingService.PostDocumentAsync` creates **Posted** journals + `DocumentGlPosted` outbox
- POS checkout: stock + payment + SalesInvoice GL (cash/bank/AR + COGS) + FBR sync with outbox retry on failure
- GRN post: inventory receive + Dr Inventory / Cr GRN Clearing
- AP invoice post: 3-way gate, posted JV (GrnClearing or Inventory vs Payable), supplier balance
- `IPaymentPostingService` customer receipts / supplier payments with Payment mappings
- Seed ensures account `1400` GRN Clearing and upserts missing account mappings

### Blazor / API UX
- Pages: GRN, AP Invoices, Quotations, Deliveries, Account Mappings, Reservations, Cycle Counts, Kits, Price Lists, Partner Aging, Receipts
- FBR history + retry; POS idempotency key; financial report CSV export
- Enterprise list/query endpoints + payment/aging/mapping/price-list APIs

### Security & ops
- JWT `branch_ids` multi-claim; `X-Branch-Id` must be in allowed set
- Warehouse lists scoped to current branch (or shared `BranchId` null)
- `AddProblemDetails` + validation ProblemDetails; lockout returns `type=account_locked`
- `POST /api/v1/auth/change-password`
- Health: `/health/live`, `/health/ready` (SQL + outbox heartbeat), `/health`
- Localization EN/UR via LocaleService + SharedResources resx; `dir=rtl` for Urdu

### Tests
- Document posting integration tests (POS/GRN/AP/period/FBR outbox)
