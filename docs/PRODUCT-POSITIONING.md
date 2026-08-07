# Product positioning — Car Auto Parts ERP

## What we claim

**Configurable mid-market ERP/POS** — ship as auto-parts, bike-parts, or general retail (stationery and similar) from one codebase via **vertical profiles** (modules, fields, branding, labels).

Honest fit:
- Catalog, warehouses, GRN → AP → payments
- POS checkout with stock + GL (+ optional FBR outbox)
- Chart of accounts, periods, journals, aging, receipts
- Roles/permissions, branch scoping, audit, backup, health checks
- Per-install business profile (Settings) without custom builds
- **Light CRM** — leads → customer convert, pipeline/kanban, activities / my-day, customer 360 (AR + sales docs), assignment rules and email template stubs (not Salesforce / marketing automation)
- **Service Light** — customer-linked service tickets with status/priority workflow, warranty queue depth + **AMC contracts**, technician **visits** + thin parts consume on ticket, mobile ticket/my-visits, **light SLA** (ticket multi-pipeline + thin ops clocks on selected docs; **admin + breach queue are Web Service module only** — WPF/POS has no SLA UI), and a browser camera barcode scan on mobile stock lookup (not a full field-service / capacity-dispatch suite)

### Light SLA scope freeze

**Bottom line:** SLA = light timers on **service tickets** (multi-pipeline policies + routing rules, first response + resolution, warn/breach, pause, escalate) **plus thin ops clocks** on selected docs (open SO age, unpaid invoice, stuck GRN/AP, low-stock reorder). It is **not** ERP-wide on every POS line, journal, or stock adjust.

Waves: [SLA-LOOP.md](SLA-LOOP.md) · [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md) · [SLA-EXPANSION.md](SLA-EXPANSION.md).

**Where SLA is**

| Area | Status |
|------|--------|
| Domain / DB / API (`/api/service/sla/*`, ticket clocks + ops clocks) | Yes |
| Background warn/breach monitor (+ low-stock sync) | Yes |
| Web: `/service/sla`, `/service/sla/breaches`, ticket strip/list, `/m/service` | Yes |
| Service multi-pipeline rules + per-policy compliance | Yes |
| Thin ops clocks: SO / unpaid invoice / GRN / AP / low-stock | Yes |
| Customer 360 badges + Dashboard breach strip + ops list badges | Yes |
| SLA-LOOP + SLA-COMPLETE-LOOP | Done |
| SLA-EXPANSION (1A + 2C) | Done |

**Where SLA is not**

| Area | Note |
|------|------|
| POS checkout lines / every stock adjust / journals | No SLA clocks |
| WPF (Presentation) | No SLA UI or code |
| CRM activities / tasks | Due dates + thin one-shot DueAt warn — **not** `SlaPolicy` |
| Knowledge base, portal, tech capacity dispatch | Internal KB stub + thin visits shipped; **portal** and capacity/map dispatch still non-goals |
| Full ServiceNow / Dynamics field-service SLA | Not claimed |

## What we do not claim (yet)

- Full SAP / Dynamics / Odoo parity
- HR / payroll, **deep** CRM (workflow builders, campaigns, multi-pipeline), e-commerce storefront
- DB-per-tenant SaaS mega-scale / multi-company verticals in one DB
- Runtime custom-field builder (EAV)
- Offline-first counter with multi-day queue (short outage queue exists as of Phase 10; not multi-day store mode; **not extended in Program C1**)
- **Full service / field-service suite** — Service Light includes light SLA timers (see [SLA-LOOP.md](SLA-LOOP.md) / [SLA-COMPLETE-LOOP.md](SLA-COMPLETE-LOOP.md)), an **internal knowledge base stub** (`/service/kb`), **technician assign + `ServiceVisit` schedule/complete**, **AMC contracts**, thin **parts consume** on ticket, and a **warranty approve/reject queue** with evidence/replacement fields — but still has **no service customer portal**, no capacity/map dispatch, and no auto RMA → SO/credit. SLA policy admin is **not** on WPF POS.
- Native mobile app or universal camera scanning — the `/m/stock` scan button relies on the browser `BarcodeDetector` API (Chromium/Android only today; no iOS Safari, no dedicated scanner hardware support)
- Masterpiece polish / category leadership — see [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) (Stages 0–5); enterprise Phases 0–11 are the foundation, not the end

## Phase baseline (current)

Enterprise hardening **Phases 0–11** plus client **Reports A–C** are shipped (see [CHANGELOG-ENTERPRISE.md](CHANGELOG-ENTERPRISE.md)). Highlights still true for go-live:

- Forced password change when admin still uses `admin123`
- Atomic POS checkout (invoice + stock + payment + GL); FBR failure does **not** roll back the sale (when FBR enabled)
- LAN: Web rewrites `ApiBaseUrl` from localhost to same host `:5280`; CORS must list the Web origin
- POS keyboard: F2 search, Enter add, F9 checkout; tender Cash/Card/Bank/Credit
- Vertical profiles, branch ACL, short offline queue, onboarding wizard — see changelog
- **Stage 0 (Phase 12 / 12.1 / 12.2):** counter polish, FBR production playbook, pilot runbook — see [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md) Q3 progress

Next product epics: remaining Stage 0 exit metrics + **Phase 13+** in [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md).

## Go-to-market language

Prefer: “Configurable mid-market ERP for auto parts, bike parts, and retail POS”  
Avoid until proven: “market masterpiece,” “complete enterprise suite forever”

## Roadmap

Climb path to **top 10–20 in-niche** (PK/South Asia auto/bike/retail multi-branch ERP+POS — not a NetSuite clone): quarter-by-quarter backlog for Stages 0–2, overview of Stages 3–5, and Phase **12+** epic names.

→ [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md)
