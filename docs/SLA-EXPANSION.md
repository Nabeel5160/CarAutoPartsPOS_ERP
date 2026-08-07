# SLA expansion (1A + 2C)

Phased reopen of the Light SLA freeze: **Service multi-pipeline** first, then **thin ops clocks** on selected docs.

## Phase 1 — Service multi-pipeline (2C) — Done

- [x] `SlaPolicyRule` routing (priority / customer type / customer / warranty) by `SortOrder`
- [x] Resolve on ticket create: rules → warranty-only policy → default
- [x] Optional policy override on ticket create (`SlaPolicyId`)
- [x] Rules CRUD API under `/api/service/sla/policies/{id}/rules`
- [x] Web `/service/sla` rules grid + per-policy compliance on dashboard
- [x] Breach queue filter by policy
- [x] Unit tests: rule match + override

## Phase 2 — Thin ops clocks (1A) — Done

- [x] `SlaEntityType` + polymorphic `SlaTimer.EntityType` / `EntityId`
- [x] `SlaPolicy.AppliesToEntityType` + default ops policies
- [x] Hooks: open SO, unpaid invoice, GRN, AP, low-stock (monitor sweep)
- [x] Complete on SO close / invoice paid / GRN-AP post / stock replenish
- [x] Breach/dashboard EntityType filters; list badges on SO / invoices / GRN / AP
- [x] Migration `SlaExpansionMultiPipelineOpsClocks` (backfill ticket timers)

## Explicitly out

- POS line / journal / every stock adjust clocks
- WPF SLA admin
- Full ServiceNow / multi-pipeline UI builders / customer portal

See [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md) for the updated freeze matrix.

**Full flow guide (how SLA works in the app):** [SLA-FLOWS.md](SLA-FLOWS.md).
