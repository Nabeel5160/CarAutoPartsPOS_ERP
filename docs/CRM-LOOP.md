# CRM loop (niche — auto parts / retail)

**Goal:** Ship usable light CRM on top of existing `Customer` (not Salesforce). Salespeople track leads → customers → follow-ups → quotes/orders without leaving the ERP.

**Wave order** matches [MASTER-ROADMAP.md](MASTER-ROADMAP.md) Phase 1 (Customer-only — no Contact entity).

| Wave | Theme | Status |
|------|-------|--------|
| **W0** | Foundation | **Done** (2026-08-02) |
| **W1** | Lead conversion | **Done** (2026-08-03) |
| **W2** | Sales pipeline | **Done** (2026-08-03) |
| **W3** | Activities / follow-ups | **Done** (2026-08-03) |
| **W4** | Customer 360 | **Done** (2026-08-03) |
| **W5** | Automation + polish | **Done** (2026-08-03) |

---

## W0 — Foundation (Done)

Entities, permissions, migration, module gate, empty CRM pages, create/list leads API.

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Domain: `Lead`, `CrmActivity`, `Opportunity` (`CompanyEntity`) | **Done** |
| 2 | Permissions: `crm.view`, `crm.manage`, `crm.leads`, `crm.activities` | **Done** |
| 3 | EF migration + DbSets + seed role templates | **Done** |
| 4 | Module key `sales.crm` + vertical KnownModules | **Done** |
| 5 | `CrmController` + `ICrmService` | **Done** |
| 6 | Web nav CRM group + pages | **Done** |
| 7 | Tests: create lead + company filter | **Done** |

### Explicit non-goals (all waves)

- Marketing campaigns / email blasts
- Full ticketing / helpdesk (Service Light = Program C)
- Separate Contact entity (Customer-only)
- Multi-pipeline Salesforce parity
- Runtime EAV custom fields

---

## W1 — Lead conversion (Done)

Convert Lead → `Customer` (idempotent), Lead → Opportunity, status machine, lost reasons, duplicate detection, owner picker, lead detail + timeline.

**Evidence:** `CrmService` convert APIs, `/crm/leads/{id}`, filters on `/crm/leads`, tests in `CrmFoundationW0Tests`.

---

## W2 — Sales pipeline (Done)

Opportunity CRUD, probability, stage history, kanban + move buttons, forecast/weighted revenue, win/lost reasons, quote link.

**Evidence:** `/crm/pipeline`, `OpportunityStageHistory`, `GET pipeline/dashboard`.

---

## W3 — Activities / follow-ups (Done)

Activity CRUD, My day / overdue / calendar list-by-day, assignee + lead/customer pickers, notifications on assign, complete + optional next follow-up.

**Evidence:** `/crm/tasks`, `INotificationService` hooks.

---

## W4 — Customer 360 (Done)

360 hub: activities, AR strip, invoices/orders/returns, profitability, converted leads. No full tickets (Program C).

**Evidence:** `GET crm/customers/{id}/360`, `/crm/customers/{id}`.

---

## W5 — Automation + polish (Done)

Assignment rules, light lead scoring, follow-up tasks on convert / Quoted, email template stubs (copy UI), mobile `/m/crm/tasks`, positioning “light CRM”, CRM smoke GET.

**Evidence:** `/crm/settings`, `CrmAssignmentRule`, `CrmEmailTemplate`, PRODUCT-POSITIONING update.
