# Technical Guide — Car Auto Parts ERP / POS

**Audience:** Developers, technical leads, DevOps, solution architects  
**Companion:** [GUIDE-BUSINESS.md](GUIDE-BUSINESS.md) (non-technical) · [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md) · [MASTER-ROADMAP.md](MASTER-ROADMAP.md)

---

## 1. Solution overview

| Layer | Project | Role |
|-------|---------|------|
| Domain | `CarAutoParts.Domain` | Entities, enums, domain rules |
| Application | `CarAutoParts.Application` | Services, DTOs, `Result<T>`, permissions, vertical profiles |
| Infrastructure | `CarAutoParts.Infrastructure` | EF Core `ApplicationDbContext`, migrations, SQL backup, seeders, outbox/FBR adapters |
| API | `CarAutoParts.Api` | ASP.NET Core JWT API, feature filters, health checks |
| Web | `CarAutoParts.Web` | Blazor WASM/host client, `CapApiService`, nav + modules |
| Desktop | `CarAutoParts.Presentation` | WPF shell (subset vs Web) |
| Tests | `tests/*` | Domain, Application, Infrastructure, **Api** (`WebApplicationFactory`) |

**Stack:** .NET 8, EF Core + SQL Server / LocalDB, Blazor, JWT auth, company-scoped multi-tenancy (shared DB, `CompanyId` filters).

**Default connection:** LocalDB `CarAutoPartsDb` — see `appsettings.json`. Snapshot copies live under `dabase/*.mdf` + `*.ldf` (git-tracked via `.gitignore` exceptions).

---

## 2. Architecture patterns

### 2.1 Layering & DI

- Application registers services in `Application/DependencyInjection.cs`.
- Infrastructure registers DbContext, repositories, `IBackupService` (SQL `BACKUP DATABASE` — **not** the removed Application placeholder).
- Controllers inherit `ApiControllerBase` → `FromResult` / `NotFoundOrOk` over `Result` / `Result<T>`.

### 2.2 Multi-company

- `CompanyEntity` + global query filters via `ICurrentCompanyContext`.
- Always verify company filter in tests with a second `CompanyId` row + `IgnoreQueryFilters` count.

### 2.3 Feature modules

- Keys in `ConfigKeys` / `ModuleKeys` (Web).
- `[RequireFeature(...)]` on controllers (404 when module off).
- Vertical defaults: `VerticalProfiles` (auto / bike / retail).

### 2.4 AuthZ

- JWT + policy names in `Permissions.cs`.
- Seed: `PermissionDefinitions` + role templates in `EnterprisePlatformSeeder`.
- Web: `PermissionService` + `NavDefinition` (permission + module gate).

### 2.5 Document posting

- Sales/POS and payments post through inventory + GL services; FBR is **outbox / non-rollback** on checkout failure.
- Approvals: `ApprovalWorkflowService` + notifications inbox.

---

## 3. Major domains (code map)

### 3.1 Catalog & inventory

| Concern | Entry points |
|---------|----------------|
| Products / brands / categories | Controllers + Web pages |
| Warehouses / locations | `WarehousesController`, locations APIs |
| Stock adjust / ATP / negative policy | Inventory services Phase 3+ |
| Transfers ship→GIT→receive | `TransferService`, `/transfers` |
| GRN / cycle count / kits | Enterprise inventory APIs |
| Serials | `SerialNumbersController` |

### 3.2 Purchasing & AP

| Concern | Entry points |
|---------|----------------|
| PR → approve → PO | `PurchaseRequisitionsController` |
| PO / receive | `PurchaseOrdersController` |
| RFQ → vendor quotes → compare → PO | `PurchaseRfqService`, `RfqController`, `/rfq` |
| AP invoices / 3-way | Enterprise purchase services |
| Supplier payment + WHT | `PaymentPostingService` (`WithholdingTaxRate` / `Amount`, GL 2210) |

### 3.3 Sales & POS

| Concern | Entry points |
|---------|----------------|
| POS checkout / holds / shifts | `PosController`, `/pos` |
| Quote → SO → delivery → invoice | `EnterpriseController` sales routes |
| Returns / credit notes | `ReturnsController` |
| FBR submissions | FBR + outbox — full flow: [FBR-INTEGRATION.md](FBR-INTEGRATION.md) |
| Sales targets | `SalesTargetService`, `/sales-targets` |
| Customer commission % | `Customer.CommissionPercent` |

### 3.4 CRM (Program A — light)

| Concern | Types / routes |
|---------|----------------|
| Entities | `Lead`, `CrmActivity`, `Opportunity`, `OpportunityStageHistory`, `CrmAssignmentRule`, `CrmEmailTemplate` |
| Service | `ICrmService` / `CrmService` |
| API | `/api/crm` + `/api/v1/crm` — leads, convert, activities, opportunities/stage, dashboard, customer 360, rules, templates, **`GET smoke`** |
| Module | `sales.crm` · Permissions `crm.view|manage|leads|activities` |
| Web | `/crm/leads`, `/crm/leads/{id}`, `/crm/tasks`, `/crm/pipeline`, `/crm/settings`, `/crm/customers/{id}`, `/m/crm/tasks` |
| Migrations | `20260802180000_CrmFoundationW0`, `20260803120000_CrmProgramA` |

**Locked product rules:** Customer-only convert (no Contact entity); W5 = light rules, not a workflow builder.

### 3.5 Service Light (Program C1)

| Concern | Types / routes |
|---------|----------------|
| Entity | `ServiceTicket` (`CompanyEntity`) |
| Service | `ServiceTicketService` |
| API | `/api/service` — CRUD, status change, customer tickets, **`GET smoke`** |
| Module | `service.tickets` · `service.view` / `service.manage` |
| Web | `/service/tickets`, `/service/tickets/{id}`, Customer 360 embed, `/m/service` |
| Mobile stock scan | `wwwroot/js/barcode-scanner.js` + `BarcodeDetector` (Chromium/Android) |
| Migration | `20260803160000_ProgramC1ServiceLight` |

### 3.6 Finance

| Concern | Entry points |
|---------|----------------|
| Periods / journals / opening | `FinanceController`, Phase4 services |
| Bank statements / recon | Phase4 + `/bank-reconciliation` |
| Cash flow | `FinancialReportService.CashFlowAsync`, `/cash-flow` |
| TB / P&amp;L / BS / aging | Enterprise report endpoints |
| Account mappings | Enterprise mappings |

### 3.7 Platform

| Concern | Entry points |
|---------|----------------|
| Users / roles / MFA | `UsersController`, `AuthController` |
| App config / modules | `AppConfigController`, Settings |
| Onboarding | `OnboardingController` |
| Audit / notifications / approvals | Dedicated controllers |
| Backup | Infrastructure `BackupService` + `BackupsController` |
| Health | `/health/ready` (+ outbox checks) |

---

## 4. API conventions

- Dual routes often exist: `/api/...` and `/api/v1/...`.
- List endpoints take `QuerySpec` (`page`, `pageSize`, `search`); **filters** are usually explicit query params merged into `query.Filters` in the controller (dictionary does not bind from query string automatically).
- Errors: ProblemDetails-style / `Result.Error` surfaced by Web `ApiClient.ExtractError`.
- Feature off → filter returns not found (treat as disabled module, not 500).

### Critical CRM routes (examples)

```
GET    /api/crm/smoke
GET    /api/crm/leads?status=&source=&ownerUserId=
POST   /api/crm/leads
POST   /api/crm/leads/{id}/convert-customer
POST   /api/crm/leads/{id}/convert-opportunity
POST   /api/crm/opportunities/{id}/stage
GET    /api/crm/pipeline/dashboard
GET    /api/crm/customers/{id}/360
```

### Critical Service routes

```
GET    /api/service/smoke
GET    /api/service/tickets
POST   /api/service/tickets
POST   /api/service/tickets/{id}/status
GET    /api/service/customers/{id}/tickets
```

Full inventory: [SMOKE-ENDPOINT-INVENTORY.md](SMOKE-ENDPOINT-INVENTORY.md).

---

## 5. Web client

- `CapApiService` wraps `ApiClient` (JSON options, auth header).
- DTOs on Web are **mutable classes** under `Web/Models` (not Application records) for Blazor binding.
- Navigation: `NavDefinition.cs` — keep permission codes and module keys in sync with seed.
- Prefer **select pickers** over raw IDs (Transfers, Requisitions, Quotations, CRM, Service).

---

## 6. Data & migrations

```bash
dotnet ef database update \
  --project src/CarAutoParts.Infrastructure \
  --startup-project src/CarAutoParts.Infrastructure
```

(Api as startup may fail if Design package is not referenced there.)

**Program migrations of note:**

| Migration | Content |
|-----------|---------|
| `...CrmFoundationW0` | Leads / activities / opportunities scaffold |
| `...CrmProgramA` | Lost/win, probability, stage history, rules, templates |
| `...ProgramBOpsGaps` | WHT columns, commission, RFQ + sales target tables |
| `...ProgramC1ServiceLight` | `ServiceTickets` |

After updating LocalDB for demos, refresh `dabase/CarAutoPartsDb.mdf` + `_log.ldf` (take DB offline → copy → online) before committing if the team tracks the snapshot.

---

## 7. Testing

| Project | Focus |
|---------|--------|
| `Application.Tests` | Service-level CRM, Service tickets, Program B (RFQ/targets/WHT), company filters |
| `Api.Tests` | `WebApplicationFactory` — CRM + Service HTTP smoke/regression |
| Domain / Infrastructure | Rules + persistence helpers |

```bash
dotnet test tests/CarAutoParts.Application.Tests
dotnet test tests/CarAutoParts.Api.Tests
```

**Known noise:** some Phase4/5 tests fail when system clock leaves hardcoded open-period fixtures (see [QA-PROGRAM-ABC.md](QA-PROGRAM-ABC.md)). Prefer date-relative periods in new tests.

QA notes: [QA-PROGRAM-ABC.md](QA-PROGRAM-ABC.md) · Smoke plans: [SMOKE-INTEGRATION-PLAN.md](SMOKE-INTEGRATION-PLAN.md).

---

## 8. Local run / LAN

1. Ensure LocalDB / SQL and connection string.  
2. Apply migrations.  
3. Run Api (default `:5280`) + Web.  
4. CORS must allow Web origin; Web rewrites `ApiBaseUrl` from localhost to LAN host when needed — [DEPLOYMENT.md](DEPLOYMENT.md).  
5. Default admin may force password change if still `admin123`.

---

## 9. Security & ops checklist

- [ ] No secrets in git (`appsettings.Development` / user secrets / env)  
- [ ] MFA policies for privileged roles as required  
- [ ] Module flags match sold SKU / vertical  
- [ ] Backup job uses Infrastructure SQL backup  
- [ ] FBR credentials only in secure config  
- [ ] Health `/health/ready` monitored in production  
- [ ] Large `dabase/*.mdf` — GitHub warns &gt;50MB; consider LFS if snapshot grows  

---

## 10. Extending the product (dev playbook)

1. **Domain entity** (+ enum) → EF configuration → migration.  
2. **DTOs + Application service** (`Result`, company check).  
3. Register DI.  
4. **Controller** with `[Authorize(Policy=...)]` + `[RequireFeature]` if module-gated.  
5. Seed permission + vertical module default.  
6. **Web** CapApiService + page + `NavDefinition`.  
7. Application test (company filter + happy/fail paths) + Api test if HTTP contract matters.  
8. Update MASTER / CHANGELOG / positioning if customer-facing claim changes.

**Program C backlog** (do not mix into ad-hoc PRs without product OK): C2 finance depth → C3 inventory depth → C4 platform → C5 AI/BI → C6 HR/mfg → C7 integrations — see MASTER Program C table.

---

## 11. Key file index

| Path | Why it matters |
|------|----------------|
| `Application/Services/CrmService.cs` | CRM behavior |
| `Application/Services/ServiceTicketService.cs` | Service Light |
| `Application/Services/PurchaseRfqService.cs` | RFQ |
| `Application/Enterprise/PaymentPostingService.cs` | WHT posting |
| `Application/Enterprise/FinancialReportService.cs` | Cash flow |
| `Api/Controllers/CrmController.cs` | CRM HTTP |
| `Api/Controllers/ServiceController.cs` | Service HTTP |
| `Infrastructure/Data/ApplicationDbContext.cs` | DbSets |
| `Web/Navigation/NavDefinition.cs` | Menus |
| `Web/Services/CapApiService.cs` | Client API surface |
| `docs/CRM-LOOP.md` | CRM wave DoD |
| `docs/MASTER-ROADMAP.md` | Gap backlog |

---

## 12. ADRs & deeper docs

- [adr/](adr/) — architecture decisions  
- [VERTICAL-PROFILES.md](VERTICAL-PROFILES.md)  
- [COSTING.md](COSTING.md) / [COA-SEED.md](COA-SEED.md)  
- [PERFORMANCE.md](PERFORMANCE.md)  
- [CHANGELOG-ENTERPRISE.md](CHANGELOG-ENTERPRISE.md)  

---

*Document version: 2026-08-03 — post Program A / B / C1.*
