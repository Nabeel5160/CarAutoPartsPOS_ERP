# API smoke scripts

Requires API on http://127.0.0.1:5280 (Development seed). Plan: [docs/SMOKE-INTEGRATION-PLAN.md](../docs/SMOKE-INTEGRATION-PLAN.md).

## Money-path baseline

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-money-path.ps1
# or: pwsh ./scripts/smoke-money-path.ps1
```

Checks: health → login → me → dashboard → COA → journals → analytics → onboarding status → approvals policies → inventory value → **timed POS products search** → **timed daily-sales**.

Elapsed ms are printed for POS search and daily-sales; compare to [docs/PERFORMANCE.md](../docs/PERFORMANCE.md).

## Waves B–H (GET-first)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-api-waves.ps1
# or: pwsh ./scripts/smoke-api-waves.ps1
```

Authenticated GETs across health/auth, catalog/inventory, POS/sales, purchasing, finance, reports, system. Prefer `CAP_SMOKE_TOKEN` if MFA blocks login. Results: [docs/SMOKE-RESULTS.md](../docs/SMOKE-RESULTS.md).
