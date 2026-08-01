# Counter UX / UI polish loop

Goal: raise Counter (POS) UX and UI polish toward **9+**. Cashiers reach `/pos` and sell without setup detours; privileged users keep governance (password / MFA).

| Wave | Theme | Status |
|------|-------|--------|
| **W0** | Kill counter friction (login → sell) | **Done** (2026-08-01) |
| **W1** | Till / shift speed + empty states | **Done** (2026-08-01) |
| **W2** | Search / cart keyboard feel | **Done** (2026-08-01) |
| **W3** | Checkout / tender / held sales clarity | **Done** (2026-08-01) |
| **W4** | Receipt / FBR / offline feedback | **Done** (2026-08-01) |
| **W5** | Visual polish + density (Phase 18 tokens) | **Done** (2026-08-01) |

---

## W0 — Kill counter friction (Done)

Cashiers must not be blocked by MFA enroll or force-password before the first sale. Seed must boot cleanly. POS first search and print must feel warm and non-blocking.

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Cashier never forced MFA before first sale | **Done** — `MfaEnrollmentPolicy` + `AuthService` / `Login.razor` → `/pos` |
| 2 | Fix seed / `ReadOnlySpan` LINQ init failure | **Done** — demo username `Contains` uses `List<string>` (not array) |
| 3 | Password change: admin-only friction | **Done** — demo users `MustChangePassword = false`; seed clears flag; admin keeps `admin123` force-change |
| 4 | POS cold start / first search warm | **Done** — catalog prefetch starts on `Pos.razor` init (parallel); focus on search; debounce kept |
| 5 | Print non-blocking + reprint + toast | **Done** — print fire-and-forget after sale; warning toast on print fail; obvious Reprint chip |

### Verify

1. **Cashier** — `cashier` / `cashier123` → lands on `/pos` (no MFA setup, no force-password). Open shift → search → sell.
2. **Sales** — `sales` / `sales123` → `/pos` (same counter path).
3. **Admin** — `admin` / `admin123` → force password change → after change, MFA enroll prompt (Skip allowed) → `/`.
4. **API boot** — no `Database initialization failed` / `ReadOnlySpan`1[System.String]` in logs; demo seed completes.
5. **POS** — open `/pos`: catalog warms (products or “Warming catalog…”); after sale, success toast; print fail shows warning without undoing sale; **Reprint last** chip visible.

### Key files

- `Application/Security/MfaEnrollmentPolicy.cs`
- `Application/Services/AuthService.cs`, `MfaService.cs`
- `Web/Pages/Login.razor`, `MfaSetup.razor`, `Pos.razor`
- `Infrastructure/Data/Seed/DemoDataSeeder.cs`, `DataSeeder.cs`
- Tests: `MfaEnrollmentPolicyTests.cs`

---

## W1 — Till / shift speed + empty states (Done)

- One-tap open shift when a single till exists (preselect already; auto-open when one till + no open shift).
- Clear empty states: no tills → `EmptyState` + onboarding link; no shift → primary CTA card.
- Remember last till in `cap.pos.lastTillId` localStorage (restore on load; save on successful open).
- Offline banner: “API down — Queue sale (F9) saves for sync”.
- Shift strip uses `cap-pos-shift-bar`; compact under 768px without removing X/Z/drop.

**DoD:** Cashier with one till reaches cart search in ≤2 clicks after login.

### Checklist notes

- [x] `cap.pos.lastTillId` restore + save
- [x] Auto `OpenShiftAsync` when `_tills.Count == 1` and no shift
- [x] `EmptyState` for no tills → `/onboarding`
- [x] Primary CTA card when shift null + tills exist
- [x] Offline banner copy tightened
- [x] `cap-pos-shift-bar` + mobile compact CSS

---

## W2 — Search / cart keyboard feel (Done)

- Debounce **140ms**; scanner Enter still bypasses debounce.
- Exact match cards get `cap-pos-exact`; supersession uses `badge text-bg-warning`.
- Warm catalog (`_warmCatalog`) filters by prefix while debounce waits; API results replace when ready.
- Cart lines show ± qty buttons; Esc clears search only; shortcuts strip always visible.

**DoD:** Scanner + keyboard path feels instant; no regression on OEM/part/fitment.

### Checklist notes

- [x] Debounce 140ms
- [x] `cap-pos-exact` + supersession badge
- [x] Warm prefix filter before API
- [x] Cart ± qty controls

---

## W3 — Checkout / tender / held sales clarity (Done)

- Cash tendered + large Change due from cart line sale prices (`UnitPrice` on `CartLine`).
- Hold toast kept; recall confirms when cart non-empty.
- Buyer / customer / FBR fields in `<details>` (closed by default; open when `_customerId` set).
- F9 while busy → warning toast “Checkout already in progress”.

**DoD:** Pay (F9) path is unambiguous; held recall cannot silently overwrite cart.

### Checklist notes

- [x] `_cashTendered` + change due
- [x] Recall `confirm` when cart has lines
- [x] Buyer/FBR collapsed in `<details>`
- [x] Busy F9 guard toast

---

## W4 — Receipt / FBR / offline feedback (Done)

- Retry print on result panel calls `PrintAsync`.
- FBR pending vs failed on success panel; **Open FBR** link when `fbr.enabled`.
- Offline queue lists keys + `lastError`; Sync now disabled while draining.
- Recent receipts: `cap.pos.recentReceipts` (last 5) + reprint menu.

**DoD:** Sale always survives print/FBR/offline failure; cashier knows next action.

### Checklist notes

- [x] Retry print → `PrintAsync`
- [x] FBR pending / failed + `/fbr` link
- [x] `_offlineItems` from `ListAsync`
- [x] Recent receipts localStorage + dropdown

---

## W5 — Visual polish + density (Done)

- Root: `cap-pos` / `cap-pos-compact` + `data-pos-contrast`.
- Density + contrast toggles near shortcuts; persist `cap.pos.density` / `cap.pos.contrast`.
- Motions: search busy sweep, cart flash on add, result fade-in; `--cap-*` tokens.

**DoD:** Counter screens pass visual review vs rest of ERP; still keyboard-first.

### Checklist notes

- [x] Density / contrast toggles + persistence
- [x] `cap-pos-search-busy`, `cap-pos-cart-flash`, `cap-pos-result`
- [x] Compact + high-contrast CSS using `--cap-*`

---

## Out of scope for this loop

- Full PWA / offline catalog DB
- Mobile POS redesign (`/m`)
- New pricing engines
