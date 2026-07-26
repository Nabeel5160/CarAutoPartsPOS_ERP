# Product positioning — Car Auto Parts ERP

## What we claim

**All-in-one buying, stocking, selling, accounting, and FBR compliance for auto-parts dealers** (mid-market, typically 1–20 branches).

Honest fit:
- Catalog, warehouses, GRN → AP → payments
- POS checkout with stock + GL + FBR outbox
- Chart of accounts, periods, journals, aging, receipts
- Roles/permissions, branch scoping, audit, backup, health checks

## What we do not claim (yet)

- Full SAP / Dynamics / Odoo parity
- HR / payroll, deep CRM, e-commerce storefront
- DB-per-tenant SaaS mega-scale
- Offline-first counter with multi-day queue (degraded messaging only in later phases)
- Masterpiece polish until Phases 1–8 of the enterprise roadmap are done

## Phase 0 baseline (current)

- Forced password change when admin still uses `admin123`
- Atomic POS checkout (invoice + stock + payment + GL); FBR failure does **not** roll back the sale
- LAN: Web rewrites `ApiBaseUrl` from localhost to same host `:5280`; CORS must list the Web origin
- POS keyboard: F2 search, Enter add, F9 checkout; tender Cash/Card/Bank/Credit

## Go-to-market language

Prefer: “Enterprise-capable mid-market auto-parts ERP”  
Avoid until proven: “market masterpiece,” “complete enterprise suite forever”
