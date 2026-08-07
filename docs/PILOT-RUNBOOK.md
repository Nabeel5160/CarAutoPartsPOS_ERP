# Pilot runbook — first 5 shops (Phase 12.2)

Use this checklist for each Stage 0 pilot (auto-parts, single branch preferred).

Related: [DEPLOYMENT.md](DEPLOYMENT.md) · [VERTICAL-PROFILES.md](VERTICAL-PROFILES.md) · [ROADMAP-TO-TOP-TIER.md](ROADMAP-TO-TOP-TIER.md)

---

## 0. Pre-flight (ops)

1. SQL Server + API + Web published per DEPLOYMENT.
2. Confirm Production config:
   - `Seed:DemoData=false` (`appsettings.Production.json` or `Seed__DemoData=false`)
   - Strong `Jwt:Key` (≥32 chars)
   - `Cors:AllowedOrigins` includes exact cashier Web origin (LAN IP if used)
3. Start API once → migrate + platform seed (COA, maps, `TILL-01`, admin). Mapping guard must not throw.
4. Health: `GET /health/live`, `GET /health/ready` (SQL + outbox heartbeat).

## 1. First login & onboarding

1. Login **admin** / **admin123** → forced password change.
2. Open `/onboarding` (auto-redirect if incomplete).
3. Set **Business type** = Auto Parts; company name, **NTN**, city, **tax %**, FBR sandbox **on** for week 1.
4. Click **Mark setup complete** — creates **TILL-01** if missing (no SQL edits).
5. Confirm steps: fiscal year, warehouse, till, COA maps (seeded). Opening balances optional until cutover.

## 2. Branch ACL (Phase 9)

1. Users → create/assign staff; set **allowed branches** + default.
2. Non-admin JWT gets `branch_ids` from ACL only (not all branches).
3. Smoke: cashier on branch HO cannot open shift on another branch’s till.
4. Financial reports: TB / P&L with `?branchId=` when multi-branch later.

## 3. Roles (Cashier / Accountant)

| Role | Template | Purpose |
|------|----------|---------|
| Cashier | Users → Apply template **Cashier** | POS checkout, shift, hold — no price override / finance post |
| Accountant | Apply **Accountant** | Finance/reports — no POS checkout |
| Admin / Manager | Existing | Approvals, settings, FBR retry |

Demo users exist only when `Seed:DemoData=true` (dev). Pilots: create real users after password policy.

## 4. Counter go-live (Phase 12)

1. POS → select **TILL-01** → Open shift.
2. Keyboard cash sale (no mouse): **F2** → scan/type known SKU → **Enter** → optional **F8** qty / **+/-** → **F9** pay.
3. Confirm receipt print; if FBR posted, IRN on receipt; **Reprint last** after any print failure.
4. If API blip: **Queue sale** / F9 offline → Sync now → reprint after drain.

## 5. FBR (sandbox first)

1. Keep sandbox until metrics look healthy on `/fbr` (success % + needs-retry).
2. Flip to prod per DEPLOYMENT **FBR sandbox → production playbook** (token + `FbrUseSandbox=false` / `Fbr__UseSandbox=false`).
3. Ops watch Dashboard FBR widgets weekly for first 5 pilots.

## 6. Backup & health

1. **Enable schedule:** Settings → Company — Auto backup on, interval hours (default 24). Confirm last backup on **/backup**.
2. **Path:** `%LocalAppData%\CarAutoParts\Backups\` on the API host; copy nightly off-box.
3. **Restore drill (staging):** once per pilot at cutover, then **D+7** — restore `.bak` to staging, `/health/ready`, open period, sample GRN→AP→POS. Log pass/fail.
4. After any restore: `/health/ready`, open fiscal period, sample GRN→AP→POS.
5. Rate limit / CORS issues → check DEPLOYMENT LAN notes. Full playbook: [DEPLOYMENT.md](DEPLOYMENT.md) § Backup / restore.

## 7. First-week support loop

For each of the **5 pilots**, track:

| Day | Check |
|-----|--------|
| D0 | Onboarding complete, one cash sale + Z-report |
| D1 | FBR list empty of stuck Failed (or Retry documented) |
| D3 | Backup file exists; health ready green |
| D7 | Cashier feedback on keyboard POS / print; triage bugs |

**Exit Stage 0 (product):** Phase 12 P0 done + pilots live or in cutover (see roadmap Q3 exit).

## Quick URLs

| Path | Use |
|------|-----|
| `/onboarding` | First-run wizard |
| `/pos` | Counter |
| `/fbr` | Submissions + metrics + retry |
| `/` | Dashboard KPIs + FBR % |
| `/users` | Roles + branch ACL |
| `/health/ready` | SQL + outbox |
