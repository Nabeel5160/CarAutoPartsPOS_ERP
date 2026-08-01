# Performance budgets — Car Auto Parts POS/ERP

Phase **19** (Stage 2). Targets are for a **typical single-branch** pilot install (≤50k active SKUs, ≤2k invoices/day peak, LAN or same-host SQL). Not a full APM product — measure with smoke timings + SQL indexes + query shaping.

Related: [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) · Phase 12 counter search · `scripts/smoke-money-path.ps1`

---

## Budgets (acceptance)

| Path | Metric | Target | Notes |
|------|--------|--------|--------|
| POS product search (exact SKU/barcode/OEM/part) | p95 latency, warm API+DB | **&lt; 1 s** | Phase 12 equality hot path + OEM/part indexes |
| POS product search (fuzzy / fitment) | p95 latency, warm | **&lt; 2 s** | Capped `Take(100)`; Contains may not use equality indexes |
| POS interactive (search → cart ready) | after warm | **&lt; 2 s** | Roadmap Stage 2 exit |
| POS checkout (single-line, no FBR wait) | p95 | **&lt; 2 s** | FBR post is async/outbox — excluded from checkout budget |
| Day sales report (JSON, ≤31 days, one branch) | p95 | **&lt; 3 s** | Aggregate queries + invoice date indexes |
| Day sales export (Excel, ≤93 days) | typical | **&lt; 8 s** | Wider export span allowed; see caps below |
| Dashboard KPI load | warm | **&lt; 3 s** | Sums/groups on invoice date |

**Cold path:** first request after process start may add JIT + connection pool warm-up; budgets apply to **warm** calls unless noted.

---

## Guardrails (code)

| Cap | Value | Where |
|-----|-------|--------|
| List API page size | max **500** | `QueryLimits.MaxPageSize` / `ToPagedResultAsync` |
| POS exact match Take | **50** | `PosCheckoutService` |
| POS soft/browse Take | **100** | `PosCheckoutService` |
| Interactive report date span | max **93** days | `ReportDateRange.ValidateInteractive` |
| Export report date span | max **366** days | `ReportDateRange.ValidateExport` |
| Stock movement rows | **5000** | `ReportService` |
| FBR register rows | **1000** | `ReportService` |

Product Excel export requests a large page but is **clamped to MaxPageSize (500)** per page — multi-page export is deferred.

---

## Indexes (hot paths)

| Table | Index | Purpose |
|-------|--------|---------|
| Products | `(CompanyId, Sku)` unique, `(CompanyId, Barcode)`, `(CompanyId, OemNumber)`, `(CompanyId, PartNumber)` | POS exact match (Phase 12+) |
| SalesInvoices | `(InvoiceDate, WarehouseId)` | Day-range / branch sales |
| SalesReturns | `(ReturnDate)` | Day sales returns filter |
| ProductVehicleCompatibilities | `(Make, Model)` | Fitment picker / filter |

Migration: `Phase19ReportAndPosIndexes`.

---

## How to measure (no cloud load farm)

1. Start API (Development seed) on `http://127.0.0.1:5280`.
2. Run timed smoke:

```powershell
pwsh ./scripts/smoke-money-path.ps1
```

The script prints **elapsed ms** for POS products search and daily-sales (today). Compare to the table above.

3. Optional SQL: enable EF sensitive logging briefly in Development and confirm POS exact search uses equality (not only `%like%`) for barcode/SKU scans.

4. Application tests: `ReportDateRangeTests`, `ClientReportsTests` (daily sales aggregates).

---

## Shipped Phase 19 wins

- Written budgets (this doc)
- Day-sales summary: `AsNoTracking` + server-side aggregates (no full invoice graph materialization)
- Report date-range validation (interactive 93 / export 366)
- Invoice / return / fitment indexes
- Smoke script timing for POS search + daily-sales
- Reports UI: skip duplicate load when URL sync fires after first paint

---

## Deferred (not Phase 19)

- Full APM (Application Insights / Datadog / OpenTelemetry collectors)
- Cloud load-test harness / k6 in CI
- Formal 50k SKU p95 lab report (Phase 12 checklist item remains open for dedicated hardware)
- Rewriting Blazor to another UI stack
- Multi-page product Excel export beyond MaxPageSize
- Dashboard query consolidation into a single SQL round-trip
