# Enterprise Mid-Market Hardening — Changelog

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
