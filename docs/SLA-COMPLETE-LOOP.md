# SLA complete loop (finish light SLA across Cap)

**Goal:** Close remaining gaps so Service SLA is production-ready everywhere Cap users work, and add thin CRM DueAt warn. Do **not** bolt SLA onto POS checkout, journals, or stock.

**Depends on:** [SLA-LOOP.md](SLA-LOOP.md) W0–W5 (**Done** — Service ticket policies/clocks/monitor/UI).

**Roadmap home:** [MASTER-ROADMAP.md](MASTER-ROADMAP.md) Phase 8.

| Wave | Theme | Status |
|------|-------|--------|
| **W0** | Harden Service SLA (ops + data) | **Done** |
| **W1** | Surface SLA in all Service surfaces | **Done** |
| **W2** | Desktop / WPF — Web-only claim | **Done** |
| **W3** | CRM activity DueAt warn (thin) | **Done** |
| **W4** | Notifications + escalation polish | **Done** |
| **W5** | QA, smoke, docs honesty | **Done** |

### Explicit non-goals

- SLA on POS sales, GRN, journals, inventory movements
- Customer self-service portal / knowledge base / field dispatch map
- Salesforce multi-policy routing / full ITSM
- Changing `DueAt` into the Service SLA engine (keep as manual hint on tickets)
- WPF Service/SLA screens (Web-only admin)

### Locked decisions

- **W2 Path A:** SLA managed in Web Service module only.
- **W3:** Thin CRM DueAt warn monitor (one notify) — no CRM `SlaPolicy` grid.

---

## Current state (after this loop)

**Claim frozen:** see [PRODUCT-POSITIONING.md](PRODUCT-POSITIONING.md) Light SLA scope freeze.

| Client / module | SLA |
|-----------------|-----|
| API + Application + DB (Service tickets) | Yes |
| Web `/service/*`, `/m/service` | Yes |
| Customer 360 / Dashboard | Yes |
| WPF / Presentation | No (by design) |
| CRM Tasks | Due dates + thin one-shot DueAt warn — not `SlaPolicy` |

---

## W0 — Harden Service SLA

| # | Item | Status |
|---|------|--------|
| 1 | Migrations apply on boot | Done |
| 2 | Default policy on first use | Done |
| 3 | Demo: warranty-only policy + escalate to manager | Done |
| 4 | Multi-company seed safe | Done |

## W1 — Surface SLA

| # | Item | Status |
|---|------|--------|
| 1 | Customer 360 ticket SLA badges | Done |
| 2 | Ticket create toast mentions SLA | Done |
| 3 | Dashboard open-breach strip | Done |
| 4 | Deep-link notifications (W4) | Done |

## W2 — Web-only claim

| # | Item | Status |
|---|------|--------|
| 1 | PRODUCT-POSITIONING / GUIDE-BUSINESS: Web-only SLA admin | Done |
| 2 | No WPF Service SLA UI | Done |

## W3 — CRM DueAt warn

| # | Item | Status |
|---|------|--------|
| 1 | `CrmActivity.SlaWarnedAt` + monitor hosted service | Done |
| 2 | One notify when due soon / overdue | Done |

## W4 — Escalation + notifications

| # | Item | Status |
|---|------|--------|
| 1 | Validate `EscalateToUserId` on policy save | Done |
| 2 | Notifications UI opens ServiceTicket | Done |

## W5 — QA / docs

| # | Item | Status |
|---|------|--------|
| 1 | Extended sla/smoke | Done |
| 2 | Tests for escalate validation + CRM warn | Done |
| 3 | CHANGELOG + wave statuses Done | Done |

---

*Created 2026-08-07 — SLA complete productization loop. Completed 2026-08-07.*
