# Pagination implementation plan

Plan + implementation waves. Audit date: 2026-07-31. Scope: Blazor grids under `src/CarAutoParts.Web/Pages`.

## Progress

| Wave | Status | Notes |
|------|--------|-------|
| W0 — Shared `Pager` | **Done** | `Components/Pager.razor` + `.cap-pager` CSS; CapApi `GetInventoryAsync` / `GetMovementsAsync` take `pageSize`. |
| W1 — High volume | **Done** | Products, Inventory, Movements, Customers, Suppliers, Audit, Serials, Transfers + Notifications (`PagedResult` API). |
| W2 — Sales docs | **Done** | Invoices, Returns UI; Quotations / SalesOrders / Deliveries API+UI. POS holds skipped. |
| W3 — Purchasing | **Done** | Purchases UI; GRN, AP invoices, Requisitions API+UI. |
| W4 — Finance | **Done** | Journals UI pager. Periods / COA / mappings / opening balances / bank recon **deferred** (small masters). |
| W5 — Masters / misc | **Done** | Users, Warehouses, Brands, Kits, Price lists, FBR + mobile `/m/stock` pager. Approvals / Categories tree / reports **deferred** or SKIP. |

### Pager conventions (W0)

- Page sizes: **25 / 50 / 100** (server clamps ≤ `QueryLimits.MaxPageSize` = 500).
- Default page size **50**.
- Label: `Page X of Y · N items · Showing A–B`.
- Reset to page 1 on search/filter.
- Reuse `PagedResult<T>` + `QuerySpec`; do not invent a second paging DTO.
- Hide pager when `TotalCount == 0`.

---

## Current state summary

| Finding | Detail |
|---------|--------|
| Shared pager UI | **None.** `Components/` has `PageHeader`, `LoadingState`, `EmptyState`, `RedirectToLogin` only — no `Pager`. |
| Server paging primitives | `PagedResult<T>` + `QuerySpec` (`Application/Common/PagedResult.cs`), `ToPagedResultAsync`, `QueryLimits.DefaultPageSize = 50`, `MaxPageSize = 500`. Web mirrors in `Web/Models/ApiModels.cs`; `ApiClient.ToQuery` emits `?page=&pageSize=&search=`. |
| Controllers already paged | Products, Inventory (+ movements), Customers, Suppliers, PurchaseOrders, Sales invoices/orders (`/api/sales/*`), Returns, Transfers, SerialNumbers, Audit, Finance journals. |
| Controllers unpaged (full/`Take` lists) | Notifications (`Take(100)`), enterprise quotations/SO/deliveries/GRN/AP/kits/price-lists/FBR, requisitions, brands, categories (tree), warehouses, users, backups, COA, periods, approvals, POS holds, reports/analytics. |
| UI reality | **Zero pages wire a pager.** Pages that call paged APIs hard-code `Page = 1` (or default `QuerySpec`) and sometimes show `TotalCount` only (Products) or a “showing N of Total — refine search” hint (MobileStock). |
| Classification | **Full (API + UI): 0** · **API-only: 12 primary grids** · **None: rest** (full list or soft-capped `Take`). |

### Existing patterns to reuse

```text
GET /api/…?page=1&pageSize=50&search=…
→ PagedResult { Items, TotalCount, Page, PageSize }  (+ TotalPages/HasNext on Application type)
```

- CapApiService already returns `PagedResult<T>` for the API-only set.
- Inventory/Movements helpers fix `pageSize=50` in the URL — extend to accept `pageSize` when adding UI.
- Journals: `GetJournalsAsync(page, pageSize)` already parameterized; UI calls defaults only.

---

## Inventory table

**Paging today:** `None` = full/capped list API + UI loads all · `API-only` = server returns `PagedResult` but UI stays on page 1 · `Full` = API + pager UI (none today).

**Priority:** P0 = POS-adjacent / high-volume masters · P1 = sales docs · P2 = purchasing · P3 = finance · P4 = low-volume masters / reports.

| Page | Route | API endpoint | Paging today | Priority |
|------|-------|--------------|--------------|----------|
| Products | `/products` | `GET /api/products` | API-only (Page=1, PageSize=50; shows TotalCount) | P0 |
| Inventory | `/inventory` | `GET /api/inventory` | API-only (page=1, pageSize=50) | P0 |
| Movements | `/movements` | `GET /api/inventory/movements` | API-only (page=1, pageSize=50) | P0 |
| Customers | `/customers` | `GET /api/customers` | API-only (QuerySpec defaults) | P0 |
| Suppliers | `/suppliers` | `GET /api/suppliers` | API-only (QuerySpec defaults) | P0 |
| Audit | `/audit` | `GET /api/audit-logs` | API-only (PageSize=100, page 1) | P0 |
| Notifications | `/notifications` | `GET /api/notifications` | None (list + server `Take(100)`; card rows, not `<table>`) | P0 |
| Serials | `/serials` | `GET /api/serial-numbers` | API-only | P0 |
| Transfers | `/transfers` | `GET /api/transfers` | API-only | P0 |
| Mobile stock | `/m/stock` | `GET /api/inventory` | API-only (hint only; out of scope for classic pager — see below) | P0* |
| Invoices | `/invoices` | `GET /api/sales/invoices` | API-only | P1 |
| Quotations | `/quotations` | `GET /api/enterprise/quotations` | None | P1 |
| Sales orders | `/sales-orders` | `GET /api/enterprise/sales-orders` | None (wholesale list; note: `/api/sales/orders` is separately paged but unused by this page) | P1 |
| Deliveries | `/deliveries` | `GET /api/enterprise/deliveries` | None | P1 |
| Returns | `/returns` | `GET /api/returns/sales` | API-only | P1 |
| POS holds | `/pos` | `GET /api/pos/holds` | None (button strip, not a grid) | P1* |
| Purchases (PO) | `/purchases` | `GET /api/purchase-orders` | API-only | P2 |
| GRN | `/grn` | `GET /api/enterprise/grn` | None | P2 |
| AP invoices | `/ap-invoices` | `GET /api/enterprise/ap-invoices` | None | P2 |
| Requisitions | `/requisitions` | `GET /api/purchase-requisitions` | None | P2 |
| Journals | `/journals` | `GET /api/v1/finance/journals` | API-only | P3 |
| Periods | `/periods` | `GET /api/v1/finance/periods` | None (small master) | P3 |
| Chart of accounts | `/coa` | `GET /api/v1/finance/coa` | None (tree/list; usually small) | P3 |
| Opening balances | `/opening-balances` | `GET /api/v1/finance/opening-balances` | None | P3 |
| Bank reconciliation | `/bank-reconciliation` | bank-statements / uncleared-gl | None | P3 |
| Account mappings | `/account-mappings` | `GET /api/enterprise/account-mappings` | None | P3 |
| Brands | `/brands` | `GET /api/brands` | None | P4 |
| Categories | `/categories` | `GET /api/categories` | None (tree — not classic page grids) | P4 |
| Warehouses | `/warehouses` | `GET /api/warehouses` (+ locations) | None | P4 |
| Kits | `/kits` | `GET /api/enterprise/kits` | None | P4 |
| Price lists | `/price-lists` | `GET /api/enterprise/price-lists` | None | P4 |
| Reservations | `/reservations` | `GET /api/enterprise/reservations` | None | P4 |
| Cycle counts | `/cycle-counts` | `GET /api/enterprise/cycle-counts` | None | P4 |
| Approvals | `/approvals` | pending + policies | None | P4 |
| Users | `/users` | `GET /api/users` | None | P4 |
| Backup | `/backup` | `GET /api/backups` | None | P4 |
| Company | `/company` | companies / branches | None | P4 |
| FBR | `/fbr` | `GET /api/enterprise/fbr/submissions` | None | P4 |
| Reorder | `/reorder` | `GET /api/purchase-requisitions/suggestions` | None (suggestion set) | P4 |
| Partner aging | `/partner-aging` | aging customers/suppliers | None (report-style) | P4 |
| Reports | `/reports` | `/api/reports/*` | SKIP — export/date-range; UI already `Take(100–300)` | — |
| Analytics | `/analytics` | `GET /api/analytics` | SKIP — capped insight lists | — |
| Financial reports | `/financial-reports` | TB / P&L / BS | SKIP — statement reports | — |
| Settings | `/settings` | settings KV table | SKIP — config, not volume | — |
| Receipts | `/receipts` | post-only forms | N/A — no list grid | — |
| Barcodes | `/barcodes` | single generate | N/A — no list grid | — |
| Dashboard | `/` | KPIs | N/A | — |
| Mobile approvals | `/m/approvals` | pending list | Out of scope (load-more later) | — |

**Grids audited (primary list surfaces):** **38** operational/master list UIs (+ mobile stock/approvals + POS holds noted).  
**Already Full:** **0** · **API-only:** **12** · **None / soft-cap:** **26+** (including notifications card list).

---

## Waves

### W0 — Shared `Pager` + conventions

**Deliverables**

1. `Components/Pager.razor` (parameters: `Page`, `PageSize`, `TotalCount`, `PageChanged`, `PageSizeChanged`; optional disabled while busy).
2. Conventions:
   - Page size options: **25 / 50 / 100** (clamp ≤ `QueryLimits.MaxPageSize`).
   - Default page size **50** (align with `QueryLimits.DefaultPageSize`).
   - Show: “Showing X–Y of Total”, Prev/Next, optional page number buttons when `TotalPages` ≤ ~10.
   - Reset to page 1 on search/filter change.
3. Optional URL query sync: `?page=&pageSize=` via `NavigationManager` / `[SupplyParameterFromQuery]` — **optional for W0**, required for deep-linkable masters in W1 if cheap.
4. Extend CapApi helpers that hard-code `pageSize=50` (`GetInventoryAsync`, `GetMovementsAsync`) to take `page` + `pageSize`.
5. Short usage note in this doc / code comment on `Pager` — do not invent a second paging DTO.

**Acceptance (W0)**

- [x] Pager renders correctly for TotalCount 0, 1 page, and multi-page fixtures.
- [x] Page size change resets to page 1 and fires a single reload callback.
- [x] No page under `Pages/` is required to adopt yet (opt-in).

---

### W1 — High-volume / POS-adjacent

| Page | Work |
|------|------|
| Products | Wire `_page` / `_pageSize`; pass `QuerySpec`; mount `Pager`. |
| Inventory | Same; keep search → page 1. |
| Customers | Same. |
| Suppliers | Same (main grid; ledger drill-down stays unpaged). |
| Audit | Same; keep filters; drop fixed PageSize=100 or make it the page-size control. |
| Movements | Same. |
| Notifications | **Add API paging** (`QuerySpec` or `page`/`pageSize` + `PagedResult`); replace soft `Take(100)` with real TotalCount; pager under card list **or** convert to table + pager. |
| Serials / Transfers | Include if capacity allows (API already ready). |

**Acceptance (W1)**

- [x] Changing page never downloads the full table for Products/Inventory/Customers/Suppliers/Audit/Movements.
- [x] `TotalCount` matches DB count under same filters; empty page shows EmptyState/LoadingState consistently.
- [x] Notifications inbox can page beyond 100 items without silent truncation.
- [x] Mobile `/m/stock` unchanged (still refine-search / future load-more). → **Updated:** simple pager wired (W5).

---

### W2 — Sales documents (+ POS holds note)

| Page | Work |
|------|------|
| Quotations | Add `PagedResult` + query params on enterprise list API; UI pager. |
| Sales orders | Same for wholesale list (or migrate page to `/api/sales/orders` if product-equivalent). |
| Deliveries | Same. |
| Invoices | UI pager only (API ready). |
| Returns | UI pager only (API ready). |
| POS holds | Optional: if hold count grows, add compact pager or “load more”; not first-class table. |

**Acceptance (W2)**

- [x] Each sales list page requests only the current page.
- [x] Enterprise list endpoints return `TotalCount` / `Page` / `PageSize` consistently with `PagedResult`.

---

### W3 — Purchasing

| Page | Work |
|------|------|
| Purchases (PO) | UI pager (API ready). |
| GRN | Add API paging + UI. |
| AP invoices | Add API paging + UI. |
| Requisitions | Add API paging + UI. |

**Acceptance (W3)**

- [x] PO/GRN/AP/PR grids page identically to W1 masters (same Pager + page sizes).

---

### W4 — Finance

| Page | Work |
|------|------|
| Journals | UI pager (API ready). |
| Periods / COA / mappings / opening balances / bank recon | Add paging **only if** lists regularly exceed ~100 rows; otherwise leave None. |

**Acceptance (W4)**

- [x] Journals paginated; other finance lists either paged or explicitly documented as “small master — no pager”.

**Deferred (small masters):** Periods, Chart of accounts, Opening balances, Bank reconciliation, Account mappings.

---

### W5 — Masters / reports note

Reports / Analytics / Financial reports / Partner aging remain **SKIP**.

W5 masters with pager: Users, Warehouses, Brands, Kits, Price lists, FBR submissions, Mobile stock.

**Deferred:** Approvals pending inbox (usually small), Categories tree, Reservations / Cycle counts (optional later), POS holds strip.

**Acceptance (W5)** — N/A for reports; masters done as above.

---

## Out of scope (this plan)

- Category **tree**, Settings KV, Receipts/Barcodes forms, Dashboard KPIs.
- POS product search (already capped via `QueryLimits.Pos*Take`).
- Changing `MaxPageSize` (500) without a perf review.
- Approvals / mobile approvals load-more (optional later).

---

## Recommended first wave

**W0–W5 shipped** (this change set).

---

## Implementation checklist (per page, when coding)

1. Keep `_page`, `_pageSize` state; load via `QuerySpec` or explicit query args.
2. Place `<Pager … />` under the table/card list.
3. On search/filter: `_page = 1` then reload.
4. Do not ignore `TotalCount` when API returns it.
5. Prefer extending CapApiService signatures over duplicating URLs in pages.
