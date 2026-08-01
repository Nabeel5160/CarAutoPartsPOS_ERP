# Smoke / integration results

**Run timestamp:** 2026-07-31 (local)  
**API base:** `http://127.0.0.1:5280`  
**Web:** `http://localhost:5156` (up; not click-through E2E)  
**Login:** `admin` / `admin123` (MFA not required this run)

> Honesty: authenticated GET coverage ≠ full UI click-through or mutating checkout/PO/GRN flows.

Plan: [SMOKE-INTEGRATION-PLAN.md](SMOKE-INTEGRATION-PLAN.md) · Inventory: [SMOKE-ENDPOINT-INVENTORY.md](SMOKE-ENDPOINT-INVENTORY.md) (**228** endpoints)

---

## Money-path baseline (`scripts/smoke-money-path.ps1`)

| Result | Count |
|--------|------:|
| PASS | 12 |
| FAIL | 0 |

All checks green (health, login, me, dashboard, COA, journals, analytics, onboarding, approval policies, inventory value, POS products search ~51ms, daily-sales ~47ms).

---

## Waves B–H (`scripts/smoke-api-waves.ps1`)

| Wave | Area | Result | PASS | FAIL | SKIP |
|------|------|--------|-----:|-----:|-----:|
| B | Health / auth | **PASS** | 4 | 0 | 0 |
| C | Catalog / inventory | **PASS** | 16 | 0 | 0 |
| D | POS / sales / wholesale | **PASS** | 15 | 0 | 1 |
| E | Purchasing | **PASS** | 5 | 0 | 0 |
| F | Finance | **PASS** | 14 | 0 | 0 |
| G | Reports | **PASS** | 17 | 0 | 0 |
| H | System / governance | **PASS** | 14 | 0 | 0 |
| **Total** | | | **85** | **0** | **1** |

### Notes

- `GET /api/pos/shifts/current` → **204** (no open shift) — treated as PASS (2xx).
- `GET /api/pos/shifts/x-report` → **400** `Open shift not found` — **SKIP** (expected without open shift; not a product defect).
- No **500** responses on canonical smoke GETs this run.

---

## Wave I — Frontend nav ↔ page ↔ API

Source: `NavDefinition.cs` leaf hrefs vs `Pages/**/*.razor` `@page` and `CapApiService` / `AuthApiService`.

| Finding | Detail |
|---------|--------|
| Pages | All major nav leaf routes have a matching `.razor` page (dashboard `/`, catalog, inventory, partners, purchasing, sales, finance, reports, analytics, system). |
| CapApiService | Paths align with inventory (`/api/...` or dual-routed `/api/v1/...` via `Ent` / finance helpers). |
| False alarm | Barcodes uses `$"/api/barcodes/{Uri.EscapeDataString(code)}"` — correct; static `{code}` string match is not a mismatch. |
| Group titles | Nav group labels (Catalog, Sales, …) are not routes — ignore. |

No CapApiService path typos found for critical pages (login / dashboard / POS).

---

## Wave J — Fix pass

| Item | Status |
|------|--------|
| API 500s on smoke GETs | None found |
| Wrong CapApiService paths | None found |
| Login / dashboard / POS null crashes | Not observed in static review; pages guard nulls / loading states |
| Smoke tooling | `PosXReport` 400 without open shift classified as **SKIP** (criteria, not product) |

**Product bugs fixed this pass:** none (environment already green).  
**Deferred:** mutating POST smoke (checkout, GRN post, journal post), full browser E2E, MFA-enrolled login automation, per-id GETs requiring seeded entities.

---

## Re-run commands

```powershell
cd CarAutoPartsPOS
$env:CAP_API_BASE = 'http://127.0.0.1:5280'
# Windows PowerShell 5.1:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-money-path.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-api-waves.ps1
# Or PowerShell 7+ if installed:
# pwsh ./scripts/smoke-money-path.ps1
# pwsh ./scripts/smoke-api-waves.ps1
```

Optional: `$env:CAP_SMOKE_TOKEN = '<bearer>'` when MFA blocks token issuance.
