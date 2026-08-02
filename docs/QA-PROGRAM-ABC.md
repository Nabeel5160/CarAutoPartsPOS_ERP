# QA Report — Program A (Light CRM) / Program B (Ops) / Program C1 (Service Light)

**Date:** 2026-08-03
**Scope:** Discover existing tests → run everything → add smoke/regression/API/integration coverage for the features below → fix any real bugs found → report.

## Summary

| Suite | Total | Passed | Failed | Notes |
|---|---|---|---|---|
| `CarAutoParts.Domain.Tests` | 8 | 8 | 0 | pre-existing, unrelated to this pass |
| `CarAutoParts.Infrastructure.Tests` | 2 | 2 | 0 | pre-existing |
| `CarAutoParts.Application.Tests` | 148 | 137 | 11 | 11 failures are pre-existing, date-sensitive period fixtures (see below); **10 new** Program B tests added in this pass, all green |
| `CarAutoParts.Api.Tests` | 13 | 13 | 0 | **new project**, all green |

No compile errors, broken DI, or missing-migration issues were found in the code under test. No feature bugs were found in Programs A/B/C1 — all added regression assertions passed against the existing implementation on the first correctly-modeled attempt.

## What was discovered

- `tests/` already had `CarAutoParts.Domain.Tests`, `CarAutoParts.Application.Tests`, `CarAutoParts.Infrastructure.Tests` (xUnit + FluentAssertions + Moq + EF Core InMemory), but **no API/HTTP-level test project** existed.
- `src/CarAutoParts.Api/Program.cs` is already a `public partial class` (ready for `WebApplicationFactory<Program>`).
- Smoke endpoints: `GET /api/crm/smoke` (lead count + open deals) and `GET /api/service/smoke` (ticket count), both permission-gated (`crm.view` / `service.view`) and module-gated (`RequireFeature`).
- Program A (CRM): leads CRUD, `convert-customer` (idempotent — repeat calls return the same `ConvertedCustomerId`), duplicate-phone detection on create, pipeline dashboard (open count/value, weighted value), assignment rules, activity/my-day, customer 360, templates — all implemented in `CrmController` / `CrmService`; existing `CrmFoundationW0Tests` already cover convert-idempotency, lost-without-reason, stage history and weighted revenue at the service layer.
- Program B (Ops): `BackupsController` placeholder already removed (confirmed via `MASTER-ROADMAP.md` + grep — no "placeholder" text remains). RFQ (`PurchaseRfqService`/`RfqController`), sales targets (`SalesTargetService`/`SalesTargetsController`), cash flow (`FinanceController`), bank reconciliation UI (`BankReconciliation.razor`), and withholding tax (`PaymentPostingService.PostSupplierPaymentAsync`) all exist and are wired, but had **no dedicated test coverage** prior to this pass (RFQ/sales-target/WHT logic isn't reachable through a simple `WebApplicationFactory` HTTP test without a much larger seed, so these were covered at the Application/service layer with mocked `IGlPostingService`/`IPurchaseOrderService` boundaries instead).
- Program C1 (Service Light): present and functional — `ServiceController`, `ServiceTicketService`, mobile `/m/service` page. Existing `ServiceTicketTests` already cover creation, company scoping, and status transitions at the service layer; this pass added the missing HTTP-level coverage.

## What ran

```
dotnet build src/CarAutoParts.Api/CarAutoParts.Api.csproj      → 0 errors
dotnet build src/CarAutoParts.Web/CarAutoParts.Web.csproj      → 0 errors
dotnet test  tests/CarAutoParts.Domain.Tests                   → 8/8 passed
dotnet test  tests/CarAutoParts.Infrastructure.Tests           → 2/2 passed
dotnet test  tests/CarAutoParts.Application.Tests              → 137/148 passed (11 pre-existing failures, see below)
dotnet test  tests/CarAutoParts.Api.Tests                      → 13/13 passed (new project)
```

(`CarAutoParts.Presentation.csproj` and the full `CarAutoParts.sln` build were skipped — the Presentation output DLL was locked by another running process unrelated to this task, and it's out of scope for Programs A/B/C1.)

## What was added

### 1. `tests/CarAutoParts.Api.Tests/` (new project)

First real end-to-end HTTP test host in the repo.

- **`CarAutoParts.Api.Tests.csproj`** — new test project referencing `CarAutoParts.Api`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, FluentAssertions, xUnit.
- **`ApiTestFactory.cs`** — `WebApplicationFactory<Program>` that swaps the SQL Server `ApplicationDbContext` for an isolated per-instance InMemory database, disables `Seed:DemoData`, and exposes `CreateAuthorizedClient(...)` to mint real signed JWTs (via the app's own `JwtTokenService`) carrying arbitrary permission/company claims — so tests exercise the **real** routing, auth, and `RequireFeatureAttribute` module-gate pipeline, not a bypassed one.
- **`CrmApiTests.cs`** (Program A, 7 tests):
  - `Smoke_Returns_Ok_With_Lead_Count_And_Open_Deals`
  - `Smoke_Without_Token_Returns_Unauthorized`
  - `Smoke_Without_CrmView_Permission_Returns_Forbidden`
  - `CreateLead_Then_List_Returns_It`
  - `ConvertLeadToCustomer_Twice_Is_Idempotent_Over_Http`
  - `PipelineDashboard_Reports_Weighted_Revenue`
  - `Lost_Without_Reason_Returns_BadRequest_Over_Http`
  - `Module_Disabled_Returns_404_For_Crm_Routes` (seeds `AppConfigEntry{Scope=Module, Key=sales.crm, Value=false}` and confirms the whole `/api/crm/*` surface 404s)
- **`ServiceApiTests.cs`** (Program C1, 5 tests):
  - `Smoke_Returns_Ok_With_Ticket_Count`
  - `CreateTicket_Then_List_Returns_It`
  - `Ticket_List_Respects_Company_Filter_Over_Http`
  - `StatusChange_To_Resolved_Requires_Resolution_Notes`
  - `Closed_Ticket_Cannot_Transition_Again_Over_Http`

Also added `Microsoft.AspNetCore.Mvc.Testing` (8.0.11) to `Directory.Packages.props`.

### 2. `tests/CarAutoParts.Application.Tests/ProgramBOpsGapsTests.cs` (new, 10 tests)

Program B service-layer regression, isolated with mocked `IGlPostingService` (GL posting) and `IPurchaseOrderService` (PO creation) so these don't depend on the accounting-period fixtures that already fail elsewhere in the suite:

- **RFQ → vendor quote → compare → PO** (`PurchaseRfqService`):
  - `CreateRfq_Requires_At_Least_One_Line`
  - `CreateRfq_Then_Send_Then_AddQuote_Moves_To_QuotesReceived`
  - `SelectVendorQuote_Deselects_Previously_Selected_Sibling`
  - `CreatePoFromQuote_Closes_Rfq_And_Links_PurchaseOrder`
- **Sales targets** (`SalesTargetService`):
  - `CreateSalesTarget_Then_Duplicate_Period_Is_Rejected`
  - `SalesTarget_Rejects_Invalid_Month_And_Negative_Amount`
  - `DeleteSalesTarget_SoftDeletes_And_Excludes_From_List`
- **Withholding tax on supplier payments** (`PaymentPostingService`):
  - `SupplierPayment_With_Wht_Posts_Net_Cash_And_WithholdingLine` (asserts the mocked GL call receives a `WithholdingTaxPayable` line and reduced `Bank` line, and that `SupplierPayment.WithholdingTaxRate/Amount` persist)
  - `SupplierPayment_Rejects_Wht_Rate_Out_Of_Range`
  - `SupplierPayment_Rejects_When_Wht_Consumes_Entire_Amount`

## Bugs fixed

None found in Programs A/B/C1 feature code. The only fixes needed were in test scaffolding while building the new `CarAutoParts.Api.Tests` project (missing `using Microsoft.Extensions.Configuration;`, an invalid `CompanyId` property on the `Customer` seed in `ServiceApiTests`, and an unnecessary `await using` on a `WebApplicationFactory` disposal) — all caught by the compiler and corrected before the first test run.

## Remaining known gaps

- **11 pre-existing failures** in `CarAutoParts.Application.Tests`, all `System.InvalidOperationException: No open accounting period for document date.`, in:
  `Phase2ProcurementTests.{Qc_hold_blocks_three_way_match_until_release, Three_way_match_allows_qty_within_tolerance}`,
  `Phase3InventoryTests.Transfer_ship_then_receive_moves_stock`,
  `Phase4FinanceTests.Sales_credit_note_apply_to_invoice`,
  `Phase5MultiBranchTests.{ShipAsync_SameBranch_SkipsGl, ShipAsync_InterBranch_PostsGitGl_AndPreservesCost}`,
  `Phase6GovernanceTests.VoidJournal_creates_reversing_entry_and_audit`,
  `DocumentPostingIntegrationTests.{Pos_checkout_posts_stock_payment_and_journal, Pos_idempotency_returns_same_invoice, Fbr_failure_enqueues_outbox, Fbr_throw_does_not_roll_back_sale}`.
  These fixtures open an accounting period for a fixed calendar date/range that no longer contains "today" as the real clock advances. They are unrelated to Programs A/B/C1 and were left as-is per the task's constraint (fix only if touched or blocking the owned suite — they do not block CRM/Service/RFQ/SalesTarget/WHT coverage, which runs independently).
- Program B items not covered by automated tests in this pass: cash flow report correctness (`FinanceController`), bank reconciliation UX (manual/UI-level, no Blazor component test harness in this repo), commission calculation (if distinct from `SalesTargetService`) — flagged for a future pass if/when a Blazor component test harness (bUnit) or a dedicated cash-flow service test target is prioritized.
- `CarAutoParts.Presentation.csproj` / full-solution build was not exercised (locked DLL from a concurrently running process, and out of scope for A/B/C1).
