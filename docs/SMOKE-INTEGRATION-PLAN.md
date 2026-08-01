# Smoke / integration plan — Car Auto Parts POS

## Why batching

The API exposes **~230** controller endpoints (see [SMOKE-ENDPOINT-INVENTORY.md](SMOKE-ENDPOINT-INVENTORY.md)). Hitting every POST/PUT/DELETE with mutating payloads is neither safe nor useful for a continuous smoke. Instead we:

1. **Inventory** all routes once (Wave A).
2. **Smoke GET-first waves** (B–H) against authenticated list/read endpoints that represent each product area.
3. **Map UI nav → pages → CapApiService paths** (Wave I) without full browser E2E.
4. **Fix pass** (Wave J) only after failures are documented.

**Honesty:** GET coverage ≠ full E2E click-through. Money-path baseline (`scripts/smoke-money-path.ps1`) still covers the critical authenticated reads used by dashboard / POS search / finance.

| Script | Role |
|--------|------|
| [scripts/smoke-money-path.ps1](../scripts/smoke-money-path.ps1) | Money-path baseline (health, login, dashboard, COA, journals, analytics, inventory value, timed POS + daily-sales) |
| [scripts/smoke-api-waves.ps1](../scripts/smoke-api-waves.ps1) | Waves B–H authenticated GETs (safe POSTs only if smoke-safe) |
| [SMOKE-RESULTS.md](SMOKE-RESULTS.md) | Timestamped run summaries |

Defaults: API `http://127.0.0.1:5280`, Web `http://localhost:5156`, login `admin` / `admin123`. If MFA is enrolled, skip MFA-gated UI; API smoke needs a bearer token (login without MFA, or set `CAP_SMOKE_TOKEN`).

---

## Waves

### Wave A — Inventory

| Item | Detail |
|------|--------|
| Goal | Complete METHOD/path/controller/auth table |
| Artifact | [SMOKE-ENDPOINT-INVENTORY.md](SMOKE-ENDPOINT-INVENTORY.md) |
| Pass | Every `*Controller.cs` represented; dual `/api` + `/api/v1` noted |

### Wave B — Health / auth

| Checks | `GET /health/live` (or `/health`), `POST /api/auth/login`, `GET /api/auth/me`, `GET /api/auth/mfa/status` |
| Pass | Live 200; login 200 with `accessToken` (or documented MFA block); me 200 with token |

### Wave C — Catalog / inventory

| Checks | products, categories, brands, warehouses, inventory list/movements/alerts/value, serial-numbers, transfers, enterprise reservations/cycle-counts/kits/price-lists |
| Pass | All GETs return 2xx (empty lists OK). 401/403 = FAIL for admin smoke. 500 = FAIL |

### Wave D — POS / sales / wholesale

| Checks | POS products/holds/shifts/tills/fitment; sales invoices/orders; returns/sales; enterprise quotations/sales-orders/deliveries/fbr metrics |
| Pass | Same as C. Mutating checkout/open-shift **not** required for smoke |

### Wave E — Purchasing

| Checks | purchase-orders, purchase-requisitions, reorder/suggestions, enterprise GRN + AP invoices |
| Pass | Same as C |

### Wave F — Finance

| Checks | finance companies/COA/periods/journals/opening-balances/bank-statements; enterprise aging + account-mappings + TB/P&amp;L/BS |
| Pass | Same as C |

### Wave G — Reports

| Checks | Representative `GET /api/reports/*` with date window (today) |
| Pass | 2xx for each; large empty exports OK |

### Wave H — System / governance

| Checks | dashboard, analytics, users, roles, settings, app-config, onboarding/status, approvals pending/policies, audit-logs, backups list, notifications |
| Pass | Same as C |

### Wave I — Frontend nav ↔ API map

| Checks | For each major `NavDefinition` href: `.razor` `@page` exists; CapApiService / AuthApiService calls align with inventory paths |
| Pass | No missing page for nav routes; mismatches listed in [SMOKE-RESULTS.md](SMOKE-RESULTS.md) (path typos / wrong prefix = bugs for Wave J) |

### Wave J — Fix pass (last)

| Scope | 500s on canonical smoke GETs; wrong CapApiService paths; null/crash on login/dashboard/pos |
| Out of scope | New features, broad refactors, full E2E automation |
| Pass | Previously failing wave checks re-run green; CHANGELOG updated for product bug fixes |

---

## Pass criteria (summary)

| Result | Meaning |
|--------|---------|
| **PASS** | HTTP 2xx (or expected 204) |
| **FAIL** | 5xx, unexpected 4xx for admin, transport error |
| **SKIP** | Needs path id / MFA / feature flag / unsafe mutation |

Wave **fails** if any non-SKIP check is FAIL. Document SKIP reasons in results.

---

## How to run

```powershell
# From repo root CarAutoPartsPOS/
$env:CAP_API_BASE = 'http://127.0.0.1:5280'
pwsh ./scripts/smoke-money-path.ps1
pwsh ./scripts/smoke-api-waves.ps1
```

Optional: `$env:CAP_SMOKE_TOKEN = '<bearer>'` if login returns MFA without token.
