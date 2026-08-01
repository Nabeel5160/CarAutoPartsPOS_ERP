# FBR production hardening — sandbox → production

Pakistan Digital Invoicing (DI) posts invoices **after** the sale commits. A failed FBR call must **never** roll back stock, payment, or GL. Retries go through the outbox / FBR page.

Related: [DEPLOYMENT.md](DEPLOYMENT.md) · [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md) · vertical module `sales.fbr` / behavior `fbr.enabled`

---

## Prerequisites

1. Vertical profile has **FBR module** and `behavior.fbr.enabled=true` (auto-parts / bike-parts presets; off for general-retail by default).
2. Company Settings: seller **NTN**, business name, province, address, **POS Id**.
3. API config section `Fbr` (env overrides use `__`):

| Setting | Sandbox | Production |
|---------|---------|------------|
| `Fbr__UseSandbox` | `true` | `false` (or leave; company `FbrUseSandbox` wins) |
| `Fbr__BearerToken` | Sandbox DI token | Production DI token |
| `Fbr__TimeoutSeconds` | `60` (default) | same |
| URLs | `PostInvoiceUrlSandbox` | `PostInvoiceUrlProduction` (defaults to FBR gw URLs) |

Company Settings **FbrUseSandbox** overrides appsettings when set — flip sandbox→prod **without a code change** by clearing sandbox on the company (or setting `FbrUseSandbox=false`) and deploying the production token.

---

## Sandbox → production checklist

1. [ ] Complete several real sales in **sandbox** (stub is OK only when token empty — not for go-live).
2. [ ] Confirm `/fbr` history shows Success (or Stub only in lab).
3. [ ] Confirm `GET /api/v1/enterprise/fbr/metrics` success rate is acceptable for the pilot window.
4. [ ] Store production Bearer token in secret store / env (`Fbr__BearerToken`) — never commit.
5. [ ] Set company **FbrUseSandbox = false** (Settings or onboarding) **or** `Fbr__UseSandbox=false`.
6. [ ] Restart API; post one controlled sale; verify FBR IRN on receipt reprint.
7. [ ] Watch outbox: failed rows → Retry on `/fbr`; processor also drains `FbrSubmissionRequested`.
8. [ ] Ops: `GET /health/ready` includes outbox heartbeat — investigate if ready fails after cutover.

---

## Non-rollback contract (verified in code)

1. POS checkout transaction commits invoice + stock + payment + GL.
2. FBR `PostInvoiceAsync` runs **after** commit.
3. Failures / exceptions → `FbrSubmission` Failed + `EnqueueFbrRetry` — sale result still returned to cashier.
4. Receipt HTML shows IRN/QR when posted; otherwise “FBR pending / failed — reprint after outbox retry”.

---

## Observability

| Signal | Where |
|--------|--------|
| Submission history | Web `/fbr` · `GET /api/v1/enterprise/fbr/submissions` |
| Success rate / needs-retry | `/fbr` metrics card · `GET /api/v1/enterprise/fbr/metrics` |
| Manual retry | `/fbr` Retry · `POST /api/v1/enterprise/fbr/retry/{invoiceId}` |
| Outbox processor | Hosted `OutboxProcessor`; readiness heartbeat |
| Module gate | Controllers use `[RequireFeature(sales.fbr)]` |

**Target (Stage 0 exit):** FBR success rate **>99%** where enabled (posted Success+Stub vs Failed+Pending, after retries).

---

## Token / NTN notes

- Empty Bearer → **stub** mode (TEST-… numbers). Fine for demos; **not** production compliance.
- NTN/CNIC and scenario IDs must match FBR enrollment for the seller.
- Vertical: disable FBR for shops that do not need DI (`fbr.enabled=false`).

---

## Rollback / incident

- Do **not** void sales solely because FBR failed — fix token/URL and retry.
- If wrong environment (sandbox token on prod URL), flip settings and re-retry failed submissions.
