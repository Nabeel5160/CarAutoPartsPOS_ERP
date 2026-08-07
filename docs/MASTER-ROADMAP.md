# Master Roadmap — ERP Growth (Target 9.5+/10)

**Lane:** Pakistan / South Asia auto-parts, bike-parts, and general retail ERP + POS (FBR where required).

**Purpose:** Map the aspirational growth roadmap against what is **already implemented** in this codebase vs what **still needs to be built**. Use this as the gap backlog. Niche climb path remains [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md); CRM waves: [CRM-LOOP.md](CRM-LOOP.md); Service SLA waves: [SLA-LOOP.md](SLA-LOOP.md); SLA productization: [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md).

**Status legend**

| Tag | Meaning |
|-----|---------|
| **DONE** | Shipped and usable in product |
| **PARTIAL** | Exists but thin, stubbed, Web-only, or missing polish |
| **TODO** | Not implemented — backlog item |

**Current overall (post Program A CRM W1–W5 + Program B P0 + Program C1 Service Light thin slice):** ~**8.3 / 10** niche · **~5.9 / 10** vs SAP/Dynamics feature surface  
**CRM maturity:** **~9 / 10** (light CRM — not Salesforce parity)

---

## How to read this doc

1. **Already implemented** — do not rebuild; harden / polish if marked PARTIAL.
2. **Still to implement** — the real backlog toward the 9.5 target.
3. Priority for *this* product: finish **CRM (Phase 1)** and close **ops gaps (backup clarity, UX, offline)** before HR / Manufacturing / full AI.

---

## Score progression (aspirational)

| Stage | Overall | vs SAP/Dynamics | Notes |
|-------|---------|-----------------|-------|
| Current (CRM W0) | 7.4 | 4.0 | Mid-market POS/ERP + CRM scaffold |
| CRM W1–W5 complete | ~8.2 | ~5.8 | Light CRM usable end-to-end |
| Inventory + Accounting + Purchasing hardened | ~8.8 | ~7.2 | Many items already DONE — focus on gaps |
| HR + Manufacturing + Service | ~9.2 | ~8.2 | Mostly greenfield today |
| BI + AI + Mobile native | ~9.5 | ~9.0 | Differentiator phase |
| Platform + Integrations + DevOps | ~9.8 | ~9.5+ | Enterprise packaging |

---

# Phase 1 — Complete CRM (Highest Priority)

**Current CRM: 3.5 → Target: 9–10** · Loop: [CRM-LOOP.md](CRM-LOOP.md)

> **Wave numbering:** Aligned with [CRM-LOOP.md](CRM-LOOP.md) — W1 convert → W2 pipeline → W3 activities → W4 360 → W5 automation (Customer-only; no Contact entity).

**Current CRM: ~9 / 10 (light)** · Overall niche ~**8.2 / 10** (post Program A + B P0)

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| CRM module toggle `sales.crm` | DONE | `ConfigKeys.ModSalesCrm`, Settings modules |
| Permissions `crm.view/manage/leads/activities` | DONE | `Permissions.cs`, role seed |
| Lead entity + convert → Customer / Opportunity | DONE | `CrmService`, `/crm/leads/{id}` |
| Duplicate detection + lost reasons + owner picker | DONE | Duplicates API + lead UI |
| Opportunity CRUD, probability, stage history, kanban | DONE | `/crm/pipeline`, dashboard |
| Activities CRUD, my-day, calendar list, notifications | DONE | `/crm/tasks` |
| Customer 360 (AR, docs, profitability; no tickets) | DONE | `/crm/customers/{id}` |
| Assignment rules, scoring, templates, mobile tasks | DONE | `/crm/settings`, `/m/crm/tasks` |
| Company-scoped CRM entities | DONE | `CompanyEntity` + query filters |
| Customer master (AR partner) | DONE | `Customer` — **not** a Contact object |

### Deferred / out of light CRM

#### W1 residual

- [x] Convert Lead → Customer (idempotent)
- [x] Convert Lead → Opportunity
- [x] ~~Convert Lead → Contact~~ — **won't do** (Customer-only decision)
- [x] Duplicate detection
- [x] Lost lead reasons
- [x] Lead status workflow UI
- [x] Required source on create (reports later)
- [x] Owner assignment UI
- [x] Activity timeline on lead detail

#### W2 — Sales pipeline — **DONE**

- [x] Kanban pipeline board (move buttons; drag-drop optional later)
- [x] Probability % per stage / deal
- [x] Expected + weighted revenue
- [x] Stage history audit
- [x] Forecast strip / dashboard
- [x] Stage colors
- [x] Lost / win reasons
- [x] Filters (stage / customer / value)
- [x] CRM dashboard cards

#### W3 — Activities — **DONE** (light)

- [x] Calls / Meetings / Emails / WhatsApp / Notes / Tasks
- [ ] Attachments on activities (schema ready; upload UX later)
- [x] Calendar list-by-day
- [x] Reminders + due notifications
- [x] Follow-up “create next” on complete (recurrence v1)
- [x] Task assignment + My day

#### W4 — Customer 360 — **DONE** (tickets deferred)

- [x] Timeline + embeds + AR + profitability + communication log
- [x] Full tickets / SLA — **DONE** (tickets C1 + SLA C2): Customer 360 tickets + [SLA-LOOP.md](SLA-LOOP.md) W0–W5; knowledge base still not implemented

#### W5 — Automation — **DONE** (light; not Salesforce builder)

- [x] Lead auto-assignment rules
- [x] Lead scoring (computed)
- [ ] Full CRM workflow engine — deferred
- [x] Email templates (copy stub; SMTP later)
- [x] Follow-up automation
- [x] CRM notifications (via inbox)
- [ ] CRM-specific approval matrix — reuse money-doc approvals only

---

# Phase 2 — Inventory (Very Important)

**Current: strong mid-market (~8) → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Multiple warehouses | DONE | Warehouses + branch scoping |
| Bin / locations | DONE | `WarehouseLocation`, Phase 15 |
| Batch / FIFO + average costing | DONE | `StockBatch`, valuation docs |
| Serial numbers | DONE | Serials module |
| Barcode generate/print | DONE | `Barcodes.razor`, barcode service |
| QR (POS/FBR receipt) | PARTIAL | Receipt/FBR QR — not general inventory QR labeling |
| Expiry on batches/GRN lines | PARTIAL | `ExpiryDate` fields exist; limited UX/reporting |
| Transfer orders (ship → in-transit → receive) | DONE | Transfers + pick list |
| Cycle count | DONE | Cycle counts + bin |
| Inventory adjustment | DONE | Inventory adjust permissions/API |
| Reorder suggestions → PR | DONE | Reorder page |
| ATP / negative-stock policy | DONE | Phase 3 |
| Reservations | DONE | Reservations page |
| Inventory valuation | DONE | Avg/FIFO reports |
| ABC analysis | DONE | Analytics |
| Dead / fast stock | DONE | Analytics / reports |
| Stock movements | DONE | Movements |
| Kits (light BOM) | PARTIAL | Product kits — not manufacturing BOM |
| Mobile stock check | PARTIAL | `/m/stock` — not full scanner app |

### Still to implement (backlog)

- [ ] Manufacturing lots (full lot genealogy beyond batch/expiry)
- [ ] Demand forecast (statistical / AI later)
- [ ] Purchase suggestions beyond reorder min/max (forecast-driven)
- [ ] Dedicated warehouse operations dashboard
- [ ] Mobile scanner app (native or hardened PWA with camera barcode)
- [ ] Inventory QR label workflow (beyond receipt QR)
- [ ] Expiry dashboards / FEFO picking policies
- [ ] Cross-warehouse ATP UX polish

---

# Phase 3 — Accounting

**Current: strong mid-market (~7–8) → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Chart of accounts | DONE | COA pages + seed |
| Journal entries | DONE | Journals + post/void |
| Cost centers | DONE | Platform entities |
| Trial balance | DONE | Financial reports |
| Profit & loss | DONE | Financial reports |
| Balance sheet | DONE | Financial reports |
| Fiscal year / accounting periods | DONE | Periods + close checklist |
| Period close / force-close | DONE | Finance permissions |
| Opening balances | DONE | Opening balances |
| Account mappings | DONE | Document → GL maps |
| Audit trail (money + audit logs) | DONE | Audit + money audit |
| Thin bank reconciliation | PARTIAL/DONE | Bank statements + recon report + suggest/auto-match (Program C2); no rules CRUD entity |
| Tax on sales/POS | PARTIAL | Tax rate / GST reports — not full tax engine |
| Multi-branch P&L / TB | DONE | Phase 9 |

### Still to implement (backlog)

- [x] Full bank reconciliation (auto-match, uncleared, rules) — **PARTIAL/DONE** (2026-08-07 Program C2): suggest + auto-match by amount/date/ref; Unclear in UI; still no match-rules entity
- [x] Cash flow statement — **PARTIAL/DONE**: `/cash-flow` page + `GET /api/v1/enterprise/reports/cash-flow` (indirect method, journals-based operating/investing/financing split) (Program B)
- [x] Budgets + budget vs actual — **DONE** (2026-08-07 Program C2): `Budget`/`BudgetLine`, `/budgets`, vs posted journals
- [ ] Full tax engine (returns, input/output, schedules)
- [x] Withholding tax (WHT) — PK-critical for B2B — **PARTIAL/DONE**: `WithholdingTaxRate`/`WithholdingTaxAmount` on supplier payments, posted to "Withholding Tax Payable" (2210) GL account, exposed on Receipts UI (Program B); not yet on purchase invoice path or WHT returns/challans
- [ ] Multi-currency GL / FX revaluation
- [ ] Deeper closing worksheets / year-end package

---

# Phase 4 — Purchasing

**Current: strong (~8) → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Purchase requisition | DONE | Requisitions |
| Approval workflow (docs) | DONE | Approvals + PR submit/approve |
| Purchase order | DONE | Purchases |
| Goods receipt (GRN) + QC | DONE | GRN |
| 3-way match | DONE | Domain invariants / AP |
| AP invoices | DONE | AP Invoices |
| Vendor / supplier returns | DONE | Returns / purchase credit |
| Reorder → PR | DONE | Reorder |
| Supplier master + ledger | DONE | Suppliers |

### Still to implement (backlog)

- [x] RFQ — **PARTIAL/DONE**: `PurchaseRfq`/`PurchaseRfqLine` entities, `RfqController`, `/rfq` page (Program B)
- [x] Vendor quotation intake — **PARTIAL/DONE**: `VendorQuote`/`VendorQuoteLine`, add-quote form on `/rfq` (Program B)
- [x] Quotation comparison matrix — **PARTIAL/DONE**: side-by-side vendor quote compare + select on `/rfq` (Program B)
- [ ] Vendor portal (external)
- [ ] Contract / blanket purchase orders
- [x] Purchasing UX: replace raw ID entry with pickers — **DONE** for `Requisitions.razor` (supplier/warehouse/product selects) (Program B)

---

# Phase 5 — Sales

**Current: strong (~8) → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Quotation | DONE | Wholesale quotations |
| Sales order | DONE | Sales orders |
| Delivery / dispatch notes | DONE | Deliveries |
| Invoice | DONE | Sales invoices + POS invoices |
| Returns + credit notes | DONE | Returns |
| Price lists | DONE | Price lists |
| POS (tender, hold, shifts, FBR) | DONE | `/pos` |
| Credit check / limits | DONE | Enterprise sales |
| Partner aging / receipts | DONE | Aging, receipts |

### Still to implement (backlog)

- [ ] Sales document approval matrix (quote/SO specific policies polish)
- [x] Delivery tracking (carrier / status / ETA) — **PARTIAL/DONE** (2026-08-07 Program C2): `Carrier`, `TrackingNumber`, `EtaUtc` on `DeliveryNote` + Deliveries UI; no carrier API
- [x] Commission engine — **PARTIAL/DONE**: `CommissionPercent` + `SalesCommission` on invoice/POS post + list API (Program C2); no GL accrual journal yet
- [x] Sales targets / quotas — **PARTIAL/DONE**: CRUD + attainment % from commission attribution (Program C2)
- [ ] Advanced discount rules (beyond price list / override)
- [ ] Customer portal (B2B self-service)
- [ ] B2B quote PDF packaging (deferred in niche roadmap)

---

# Phase 6 — HRMS

**Current: ~0–1 → Target: 9**

### Already implemented

| Item | Status | Notes |
|------|--------|-------|
| App users / roles | DONE | Auth users — **not** employees/HR |

### Still to implement (backlog) — all greenfield

- [ ] Attendance
- [ ] Leave
- [ ] Payroll
- [ ] Shift (HR shifts ≠ POS cashier shifts)
- [ ] Recruitment
- [ ] Employee documents
- [ ] Performance
- [ ] Expense claims
- [ ] Travel requests
- [ ] Assets (HR/fixed assets)
- [ ] Training

> **Product call:** HRMS is out of niche positioning today. Schedule only after CRM + ops maturity, or spin as optional module.

---

# Phase 7 — Manufacturing

**Current: ~0–1 (kits only) → Target: 9**

### Already implemented

| Item | Status | Notes |
|------|--------|-------|
| Product kits | PARTIAL | Assemble components — not MRP/shop floor |

### Still to implement (backlog)

- [ ] Full BOM (multi-level)
- [ ] Work orders
- [ ] Production planning
- [ ] Machine scheduling
- [ ] Quality control
- [ ] Material consumption posting
- [ ] Production cost
- [ ] Shop floor terminals
- [ ] MRP
- [ ] Capacity planning

---

# Phase 8 — Service Management

**Current: ~2 (Service Light thin slice) → Target: 9** · SLA: [SLA-LOOP.md](SLA-LOOP.md) · Complete: [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md)

> **SLA wave numbering:** Core timers: [SLA-LOOP.md](SLA-LOOP.md) W0–W5 (**Done**). Productization: [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md) W0–W5 (**Done** — 360/dashboard/CRM DueAt warn/Web-only claim).

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Service tickets (CRUD, status workflow, priority) | PARTIAL/DONE | `ServiceTicket` entity, `ServiceController`, `/service/tickets` (Program C1) |
| Warranty / AMC reference on ticket | PARTIAL/DONE | Free-text refs kept; `AmcContract` + `AmcContractId`; warranty queue + evidence/replacement fields (no portal) |
| Ticket ↔ Customer 360 link | PARTIAL/DONE | Tickets card on `/crm/customers/{id}` (Program C1) |
| Mobile ticket list / resolve | PARTIAL/DONE | `/m/service` — list + status/notes update (Program C1) |
| Technician assignment (filter + notify) | DONE | Ticket assignee filter (Mine/Unassigned/user); notify on create/reassign |
| Warranty claim approve/reject + queue | PARTIAL/DONE | Queue + decide API; evidence notes, invoice link, replacement product/qty on approve; reject requires notes — no auto SO/credit |
| Knowledge base (internal articles stub) | PARTIAL/DONE | `KbArticle`, `/service/kb`, ticket detail related links — not customer portal |
| SLA timers / breach alerts | DONE | [SLA-LOOP.md](SLA-LOOP.md) W0–W5 — policies, clocks, monitor, pause, UI (Program C2) |
| SLA multi-pipeline + thin ops clocks | DONE | [SLA-EXPANSION.md](SLA-EXPANSION.md) — rules routing; SO/invoice/GRN/AP/low-stock clocks |
| SLA scope freeze (Web admin; no WPF; no POS-line/journal) | DONE | [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md) Light SLA scope freeze matrix |
| CRM activity DueAt warn (thin) | DONE | One-shot notify — **not** Service SLA / `SlaPolicy` ([SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md) W3) |

### Still to implement (backlog)

- [x] SLA timers / breach alerts — **Done** (2026-08-07): [SLA-LOOP.md](SLA-LOOP.md) W0–W5 — policies, clocks, monitor, pause/resume, UI, business calendar, dashboard
- [x] Knowledge base — **PARTIAL** (2026-08-07): internal `KbArticle` CRUD + ticket related links; **not** customer portal
- [ ] Customer portal (service)
- [x] Technician assignment / scheduling — **PARTIAL/DONE** (2026-08-07 Phase 8 depth): assign + `ServiceVisit` schedule/complete + mobile my-visits; no capacity calendar
- [x] Field service (visits, parts consumption on ticket) — **PARTIAL/DONE** (2026-08-07): visits + `ServiceTicketPart` consume via inventory deduct
- [x] Dedicated warranty claim workflow — **PARTIAL/DONE** (2026-08-07): approve/reject + evidence/invoice/replacement fields; no auto SO/credit
- [x] AMC contract entity (recurring coverage, renewal, billing) — **DONE** (2026-08-07): `AmcContract` + ticket `AmcContractId`; billing/renewal automation still thin (manual status/dates)

> Parts shops often need **warranty + simple tickets** before full field service. **Program C1** shipped tickets; **Program C2** light SLA ([SLA-LOOP.md](SLA-LOOP.md)) + complete loop ([SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md)). See `docs/PRODUCT-POSITIONING.md`.

---

# Phase 9 — Reports & BI

**Current: ~7–8 → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Executive / sales dashboards | DONE | Dashboard + analytics |
| Finance reports (TB/P&L/BS) | DONE | FinancialReports |
| Inventory insights | DONE | Analytics (ABC, dead/fast, GM) |
| Export Excel / PDF | DONE | ClosedXML / QuestPDF |
| Client reports A–C | DONE | Reports roadmap |
| Animated charts | DONE | ECharts CapChart |

### Still to implement (backlog)

- [ ] Dedicated CRM dashboard (after W2)
- [ ] HR dashboard (after HRMS)
- [ ] Scheduled email report packs
- [ ] Manager week PDF pack
- [ ] Power BI connector / semantic model
- [ ] Formal 50k-SKU performance lab evidence

---

# Phase 10 — AI (USP)

**Current: 0 → Target: differentiator**

### Already implemented

_None._

### Still to implement (backlog)

- [ ] AI sales forecast
- [ ] AI inventory forecast
- [ ] AI purchase suggestions
- [ ] AI chat assistant
- [ ] AI invoice / receipt OCR
- [ ] AI document search
- [ ] AI customer insights
- [ ] AI lead scoring
- [ ] AI auto replies
- [ ] AI report generator
- [ ] Natural language search
- [ ] AI dashboard

---

# Phase 11 — Mobile Apps

**Current: ~5.5 (web light + camera scan) → Target: 9**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Mobile light web (`/m`) | PARTIAL | Stock + approvals + CRM tasks + service hub |
| Responsive layouts | PARTIAL | Many pages; uneven |
| Short offline POS queue | PARTIAL | Max ~100 / 24h — not multi-day |
| Service tickets mobile view | PARTIAL/DONE | `/m/service` — list, filter, status change (Program C1) |
| Camera barcode scanning | PARTIAL | `/m/stock` "Scan barcode" button uses browser `BarcodeDetector` API (Chrome/Edge Android only; no iOS Safari support, no native fallback) (Program C1) |

### Still to implement (backlog)

- [ ] Sales native / PWA app
- [ ] Warehouse app + barcode scanner — **PARTIAL**: one scan entry point on `/m/stock` only, not a full scanner workflow (receive/pick/count)
- [ ] Approval app (beyond `/m/approvals`)
- [ ] CRM mobile app
- [ ] Manager app
- [ ] True offline support (Phase 20 extended offline) — **not attempted in Program C1**; existing short POS queue is unchanged
- [ ] Push notifications
- [ ] Camera barcode scanning on browsers without `BarcodeDetector` (iOS Safari, older Android) — needs a JS polyfill/library

---

# Phase 12 — Platform Features

**Current: ~7–8 → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Audit logs | DONE | Audit module |
| Notification center | DONE | Notifications |
| Roles + fine-grained permissions | DONE | ~65+ permissions |
| Approval policies | DONE | Approvals |
| Multi-company (row filter) | PARTIAL | Install-first multi-company — not SaaS |
| Multi-branch + ACL | DONE | Phase 5/9 |
| Localization EN/UR | PARTIAL | Labels; not full i18n everywhere |
| Vertical profiles / feature flags (modules) | DONE | `AppConfigEntries` |
| Branding / theme tokens | PARTIAL | Brand + UI theme |
| MFA | DONE | TOTP |
| Outbox | DONE | FBR/GL side effects |

### Still to implement (backlog)

- [ ] Visual workflow builder
- [ ] Role builder UI (beyond seed templates)
- [ ] Runtime custom fields (EAV)
- [ ] Form / page / report builders
- [ ] API keys
- [ ] Webhook engine
- [ ] Third-party integration hub
- [ ] Theme marketplace / white-label packager
- [ ] Plugin system
- [ ] Full multi-currency
- [ ] Full multi-language packs
- [ ] DB-per-tenant / SaaS tenancy

---

# Phase 13 — DevOps & Production Readiness

**Current: ~6.5–7.5 → Target: 10**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| SQL `BACKUP DATABASE` service | DONE | `Infrastructure/Services/BackupService.cs` registered in DI |
| Application `BackupService` placeholder | DONE (removed) | Dead duplicate removed from `Application/Services/SettingsServices.cs`; `IBackupService` interface kept, DI unaffected (Program B) |
| Health checks | DONE | `/health/live`, `/health/ready` |
| Rate limiting | DONE | API Program |
| CI (build + tests) | PARTIAL | `.github/workflows/ci.yml` — Infra tests skipped |
| Serilog logging | DONE | Infrastructure |
| Transactional outbox / queue | PARTIAL | Outbox processor — not general job queue |
| Smoke scripts | DONE | `scripts/smoke-*.ps1` |
| Feature flags (modules) | DONE | Vertical modules |

### Still to implement (backlog)

- [x] Remove / consolidate Application-layer backup **placeholder** class (dead code risk) — **DONE** (Program B)
- [x] Automated backup scheduling ops playbook + restore drills — **DONE** (2026-08-07): Settings `AutoBackup*` UI + DEPLOYMENT/PILOT restore-drill playbook (D+7); hosted service already existed
- [ ] Monitoring / APM / alerting
- [ ] Performance dashboard in ops
- [ ] Distributed cache strategy
- [ ] General purpose job queue
- [ ] Disaster recovery runbooks
- [ ] Security scanning + pen test cadence
- [ ] Zero-downtime deployment
- [ ] Formal load tests (50k SKU)

---

# Phase 14 — UX/UI

**Current: ~6 → Target: 9.5**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Dark / light theme | DONE | `ThemeService` + Settings |
| Responsive (partial) | PARTIAL | Mobile pages + some grids |
| POS keyboard shortcuts | DONE | F2 / Enter / F9 |
| Design tokens / empty states | PARTIAL | Phase 18 pragmatic |
| WPF global search | DONE | Presentation shell only |
| Web global search | PARTIAL/DONE | Topbar MVP (2026-08-07); command palette deferred |
| Shell sidebar / fullscreen | DONE | MainLayout |

### Still to implement (backlog)

- [x] Web global search — **DONE** (2026-08-07): topbar MVP (products/customers/suppliers/POs/invoices); command palette still open
- [ ] Command palette
- [ ] Quick actions / favorites / pinned / recent records
- [ ] Customizable dashboard widgets
- [ ] Advanced search everywhere
- [x] Replace raw numeric ID forms with searchable pickers — **PARTIAL/DONE** (2026-08-07 Ops/UX): Returns, GRN, Inventory, Purchases, AP, Deliveries, POS warehouse/customer, Opening balances, Receipts (+ prior Program B pages); CycleCounts/Serials/etc. still raw
- [ ] Guided tour
- [ ] Accessibility pass (WCAG)
- [ ] Counter UX already looped — keep polishing CRM/admin UX similarly

---

# Phase 15 — Enterprise Integrations

**Current: FBR strong; rest thin → Target: enterprise ready**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| FBR Digital Invoicing | DONE | POS + FBR module + outbox |
| Excel import/export | PARTIAL | Product CSV / report exports |
| Email (transactional) | MISSING / TBD | No first-class email server integration |

### Still to implement (backlog)

- [ ] WhatsApp Business API
- [ ] SMS gateway
- [ ] Email server (SMTP / provider)
- [ ] Stripe / PayPal
- [ ] JazzCash / Easypaisa
- [ ] FedEx / DHL
- [ ] Google Calendar / Outlook
- [ ] Teams / Slack
- [ ] Power BI
- [ ] SAP import bridge
- [ ] Richer Excel import center

---

# Phase 16 — Industry Solutions

**Current: 3 verticals → Target: market expansion**

### Already implemented

| Item | Status | Evidence |
|------|--------|----------|
| Auto-parts vertical | DONE | Default profile |
| Bike-parts vertical | DONE | VerticalProfiles |
| General retail vertical | DONE | VerticalProfiles |
| Retail POS | DONE | POS module |

### Still to implement (backlog)

- [ ] Healthcare
- [ ] Pharmacy
- [ ] Construction
- [ ] Education
- [ ] Real estate
- [ ] Restaurant (deeper than retail)
- [ ] Manufacturing vertical pack
- [ ] Textile
- [ ] Wholesale distribution pack (beyond current O2C)
- [ ] Automobile dealer pack (beyond parts)

---

# Recommended build sequence (honest)

Do **not** start Phases 6–8, 10, or 16 expansion until the niche core is airtight.

```text
1. CRM W1 → W5          DONE (Program A — light CRM)
2. Ops polish            DONE/PARTIAL (pickers wave, backup Settings+playbook, web global search MVP; command palette open)
3. Accounting gaps       DONE/PARTIAL (WHT, cash flow, bank recon match UX)
4. Purchasing gaps       DONE/PARTIAL (RFQ → compare → PO)
5. Sales gaps            DONE/PARTIAL (commission %, sales targets; portals later)
6. Program C1            DONE/PARTIAL (Service Light tickets + mobile + camera scan)
7. Program C2–C7 quarters (finance depth, AI, HR/mfg — see below)
```

---

# Program C — Multi-quarter remainder (C1 started, C2–C7 outline only)

**C2–C7: do not implement until each prior quarter is accepted.** Track here only; schedule as separate epics.

| Quarter | MASTER phases | Focus | Status |
|---------|---------------|--------|--------|
| **C1** Service Light + Mobile | 8 thin, 11 | Tickets / warranty-AMC reference / mobile ticket view / camera scan | **PARTIAL/DONE** — see Phase 8 & 11 above; extended (multi-day) offline **not attempted** |
| **C2** Finance / Sales depth | 3–5 remainders | Budgets, bank auto-match, commission/targets, delivery tracking | **PARTIAL/DONE** (2026-08-07) — budgets + bank suggest/auto-match + commission/attainment + delivery tracking; multi-currency / portals / discount rules still TODO |
| **C3** Inventory depth | 2 remainders | FEFO, WH dashboard, demand forecast | TODO |
| **C4** Platform | 12 | Webhooks, API keys, EAV custom fields (if still desired) | TODO |
| **C5** AI + BI | 9–10 | Forecasts, assistant, Power BI connector | TODO |
| **C6** Optional modules | 6–7, 16 | HRMS, manufacturing, new industry packs | TODO |
| **C7** Integrations / DevOps | 13, 15 | JazzCash / WhatsApp / SMS, APM, DR, CD | TODO |

---

# Why SAP/Dynamics comparison was low (~4.0 → ~5.8 after A+B)

Not a judgment on code quality. Enterprise buyers score **capability breadth**:

- End-to-end CRM (lead → quote → order → invoice → support)
- Advanced finance (budget, WHT, cash flow, multi-currency)
- Manufacturing MRP / shop floor
- Service / field service
- Workflow automation builders
- BI + forecasting
- Marketplace / ecosystem
- Integrations
- Scale + ops tooling

This architecture can grow into those areas. Closing the **Still to implement** lists above is what moves the comparison score.

---

# Related docs

| Doc | Role |
|-----|------|
| [CRM-LOOP.md](CRM-LOOP.md) | CRM W0–W5 execution loop |
| [SLA-LOOP.md](SLA-LOOP.md) | Service SLA W0–W5 (Phase 8 timers / breach) |
| [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md) | SLA productization (360, dashboard, CRM warn, Web-only) |
| [SLA-EXPANSION.md](SLA-EXPANSION.md) | Multi-pipeline + thin ops clocks (1A + 2C) |
| [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) | Niche top-20 climb (Stages 0–5) |
| [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md) | What we claim / do not claim |
| [PHASE-COMPLETION-PLAN.md](PHASE-COMPLETION-PLAN.md) | Finish parallel enterprise phases |
| [CHANGELOG-ENTERPRISE.md](CHANGELOG-ENTERPRISE.md) | What already shipped |
| [VERTICAL-PROFILES.md](VERTICAL-PROFILES.md) | Auto / bike / retail config |

---

*Last audited: 2026-08-07 — Program A (CRM) + B P0 + C1 (Service Light) + light SLA + Program C2 finance/sales depth (budgets, bank auto-match, commission/targets, delivery tracking); multi-currency / portals / discount rules remain TODO.*
