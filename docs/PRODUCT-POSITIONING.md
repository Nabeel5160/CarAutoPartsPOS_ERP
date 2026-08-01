# Product positioning — Car Auto Parts ERP

## What we claim

**Configurable mid-market ERP/POS** — ship as auto-parts, bike-parts, or general retail (stationery and similar) from one codebase via **vertical profiles** (modules, fields, branding, labels).

Honest fit:
- Catalog, warehouses, GRN → AP → payments
- POS checkout with stock + GL (+ optional FBR outbox)
- Chart of accounts, periods, journals, aging, receipts
- Roles/permissions, branch scoping, audit, backup, health checks
- Per-install business profile (Settings) without custom builds

## What we do not claim (yet)

- Full SAP / Dynamics / Odoo parity
- HR / payroll, deep CRM, e-commerce storefront
- DB-per-tenant SaaS mega-scale / multi-company verticals in one DB
- Runtime custom-field builder (EAV)
- Offline-first counter with multi-day queue (short outage queue exists as of Phase 10; not multi-day store mode)
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
