# Client Reports Roadmap (gaps only)

Based on codebase audit of Web/API vs mid-market POS/ERP client needs. Existing TB/P&L/BS, AR/AP aging screens, ledgers, analytics KPIs, reorder, low-stock, profit-by-product Excel, and Z-calc logic are **in scope to reuse — not rebuild**.

## Already covered (skip rebuild)

- Financial: Trial balance, P&L, balance sheet ([FinancialReports.razor](src/CarAutoParts.Web/Pages/FinancialReports.razor))
- AR/AP aging screens, customer/supplier ledgers
- Inventory valuation KPI + Avg/FIFO value API; low/overstock alerts; reorder → draft PR
- Analytics: dead/slow/fast, ABC, GM (screen) — only add **export** later
- Excel: sales/inventory/purchases/profit header exports ([Reports.razor](src/CarAutoParts.Web/Pages/Reports.razor))
- POS receipt HTML; shift Z **calculation** (reuse DTO)
- FBR history/retry (extend to register only when FBR on)

## Shipped (Phases A–C) — 2026-07-30

### Phase A
| Item | Route / API | Notes |
|------|-------------|--------|
| Daily sales summary | `/reports?type=daily-sales` · `GET /api/reports/daily-sales` | Grid + Excel + PDF; tenders/tax/returns; branch ACL; `tax.enabled` |
| Shift Z archive | `/reports?type=z-shifts` | Closed shifts list + Excel + printable Z via existing `GetZReportAsync` |
| X-report | POS **X-report** · `GET /api/pos/shifts/x-report` | Open-shift snapshot without close |
| Sales returns period | `/reports?type=sales-returns` | Screen + Excel |
| Branch-scope exports | sales/inventory/purchases/profit `?branchId=` | `IsBranchAllowed` |
| Product vs Stock nav | Product nav removed; **Sales dim** covers product tab | Stock kept |

### Phase B
| Item | Route / API |
|------|-------------|
| Sales by product/category/brand | `/reports?type=sales-dim` (+ OEM when field on) |
| Sales by cashier/till/branch | `/reports?type=sales-staff` |
| Profit by category/branch | `/reports?type=profit-dim` (+ profit tab export) |
| Analytics Excel | Analytics **Export XLSX** · `GET /api/reports/analytics-export` |
| Stock movements | `/reports?type=movements` |
| Open PO / pending GRN | `/reports?type=purchasing-pipeline` |
| AR/AP aging Excel | `/reports?type=aging` + Partner Aging buttons · `GET /api/reports/aging` |

### Phase C
| Item | Route / API | Notes |
|------|-------------|--------|
| Tax/GST period | `/reports?type=tax` | HS when `product.hsCode` visible |
| FBR register | `/reports?type=fbr` | Nav module `sales.fbr` only |
| Stock aging | `/reports?type=stock-aging` | Best-effort: `StockBatch.ReceivedDate`, else last inbound movement / `CreatedAt` |
| Real PDF | `format=pdf` on inventory/sales/purchases/daily-sales | QuestPDF; branch ACL parity with Excel (Phase 16) |
| SKU margin | `/reports?type=sku-margin` | Optional purchase-vs-sales margin by SKU |

### Tests
- `ClientReportsTests` — daily sales totals + branch deny + branch-scoped sales filter
- `Phase16ReportCadenceTests` — shared `ReportBranchScope` ACL parity + Z archive Excel + PDF deny (QuestPDF render not in-process — native host crash risk)

## Known limitations

- Stock aging without batches falls back to last positive movement / inventory created date (not true GRN layering).
- Scheduled email report packs and manager week PDF pack are Stage 1 later quarters (see [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) Phase 16).
- Product catalog Excel duplicate removed; use Stock or Sales dim.

## Phase 16 — Report Cadence (2026-07-31)

| Item | Status | Notes |
|------|--------|--------|
| PDF branch ACL parity | Done | Inventory/sales/purchases PDFs use same warehouse/`branch_ids` scoping as Excel |
| Z archive Excel | Done | `GET /api/reports/z-shifts?format=xlsx` + Reports Export XLSX |
| Scheduled email packs | Deferred | Q1 2027 P1 |
| Manager week PDF pack | Deferred | Q2 2027 P2 |

## Cross-cutting rules

- Place new items under the **Reports** parent nav ([NavDefinition.cs](src/CarAutoParts.Web/Navigation/NavDefinition.cs)).
- Prefer **on-screen grids** for Phase A (not Excel-only mid-shift).
- Branch ACL on every new report (match Analytics / Financial Reports).
- Hide FBR/OEM/HS columns via vertical profile toggles.
- Module key: `insights.reports` (+ permission `reports.export`).
