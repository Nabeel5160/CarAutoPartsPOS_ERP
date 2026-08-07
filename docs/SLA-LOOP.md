# SLA loop (Service Light → real SLA)

**Goal:** Add a proper **SLA service layer** on top of Program C1 tickets — policies, clocks, pause/resume, warning/breach, escalation — without turning the product into ServiceNow/Dynamics Field Service.

**Market model (best practice):** ITIL / Zendesk / Dynamics / ServiceNow pattern  
**Policy → attach on ticket open → run clocks (start/pause/stop) → warn → breach → escalate → measure**

**Roadmap home:** [MASTER-ROADMAP.md](MASTER-ROADMAP.md) Phase 8 — Service Management.

**Depends on:** Program C1 Service Light (`ServiceTicket`, `/api/service`, `/service/tickets`).

| Wave | Theme | Status |
|------|-------|--------|
| **W0** | Domain + policy foundation | **Done** (2026-08-07) |
| **W1** | Clock engine on ticket lifecycle | **Done** (2026-08-07) |
| **W2** | Warn / breach / notify | **Done** (2026-08-07) |
| **W3** | Escalation + pause reasons | **Done** (2026-08-07) |
| **W4** | UI (settings + ticket badges + breach queue) | **Done** (2026-08-07) |
| **W5** | Metrics, calendar polish, smoke/tests | **Done** (2026-08-07) |

### Explicit non-goals (all waves)

- Full field-service dispatch / tech calendar
- Customer self-service portal
- Knowledge base
- Dedicated warranty-claim / AMC-contract entities (keep free-text refs for now)
- Multi-pipeline Salesforce-style SLA suites

---

## Architecture (Clean Architecture — match Cap)

```
┌─────────────────────────────────────────────────────────┐
│ Web / Mobile                                            │
│  /service/sla (policies) · ticket SLA strip · breach Q  │
└───────────────────────────┬─────────────────────────────┘
                            │ CapApiService
┌───────────────────────────▼─────────────────────────────┐
│ Api  ServiceController (+ /sla/*) or SlaController      │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ Application                                             │
│  ISlaPolicyService   — CRUD policies / targets          │
│  ISlaClockService    — start/pause/resume/stop/evaluate │
│  ISlaMonitorService  — scan approaching/breached        │
│  hooks from ServiceTicketService (create / status)      │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ Domain                                                  │
│  SlaPolicy · SlaTarget · SlaTimer · SlaEvent            │
│  BusinessCalendar (optional W5)                         │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ Infrastructure                                          │
│  EF entities/migrations · SlaMonitorBackgroundService   │
│  reuse INotificationService (+ Outbox if needed)        │
└─────────────────────────────────────────────────────────┘
```

**Do not** put SLA math only in Razor or only as `DueAt`. `DueAt` stays a **manual hint**; SLA clocks are **policy-driven** and auditable.

---

## Market practices to encode

| Practice | How we apply it |
|----------|-----------------|
| Separate **First Response** vs **Resolution** SLAs | Two metric types on one policy |
| Targets by **priority** (and later customer tier) | `SlaTarget` rows: Priority → minutes |
| Clock **pause** when waiting on customer / parts | Status or pause reason stops resolution clock |
| **Business hours** (not 24×7 by default for parts shops) | Calendar: Mon–Sat shop hours; holidays later |
| **Warn %** before breach (e.g. 80%) | `WarnAtPercent` on target |
| **Breach is sticky** until resolved/closed | Status `Ok → Warning → Breached`; never silently clear breach history |
| **Escalation** = notify manager / reassign rule | Reuse notifications; optional assignee bump |
| **Company-scoped** policies | All entities `CompanyEntity` like tickets |
| Measure **compliance %** | Resolved within target / total resolved |

---

## Domain model (proposed)

```text
SlaPolicy (CompanyEntity)
  Name, IsDefault, IsActive
  CalendarMode: AlwaysOn | BusinessHours
  ApplyToWarrantyOnly? (bool, optional)

SlaTarget
  SlaPolicyId
  Metric: FirstResponse | Resolution
  Priority: Low|Normal|High|Urgent  (match ServiceTicketPriority)
  TargetMinutes
  WarnAtPercent (default 80)

SlaTimer  (one row per ticket × metric)
  ServiceTicketId, Metric
  SlaPolicyId, SlaTargetId
  Status: Running | Paused | Met | Breached | Cancelled
  StartedAt, PausedAt, CompletedAt
  ElapsedSeconds (accumulated; exclude pauses)
  TargetSeconds, WarnSeconds
  BreachedAt?, WarnedAt?
  FirstResponseAt? (ticket-level helper also OK)

SlaEvent (audit)
  SlaTimerId, At, Kind: Started|Paused|Resumed|Warned|Breached|Met|Cancelled
  Note?

BusinessCalendar (W5)
  CompanyId, TimeZoneId, WorkIntervals, Holidays
```

**Ticket link (minimal):** either FK `ServiceTicket.SlaPolicyId` or always resolve “active default policy” on open. Prefer storing `SlaPolicyId` on ticket for audit stability if policy changes later.

---

## Workflow (runtime)

```text
Ticket Created (Open)
  → resolve SlaPolicy (default / warranty rule)
  → create SlaTimer(FirstResponse) + SlaTimer(Resolution)  [Running]
  → compute TargetDue ≈ StartedAt + business minutes

First staff note / status → InProgress  (or dedicated “Responded”)
  → stop FirstResponse timer → Met or Breached
  → Resolution keeps Running

Status → waiting (Paused) / parts pending
  → pause Resolution; record SlaEvent

Status → InProgress again
  → resume Resolution

Status → Resolved / Closed
  → stop Resolution → Met or Breached
  → cancel any still-open FirstResponse if never responded

Background every N minutes
  → for Running timers: recompute elapsed (business calendar)
  → if elapsed ≥ WarnSeconds and not Warned → notify + Warned
  → if elapsed ≥ TargetSeconds → Breached + escalate
```

**First response definition (pick one and document):**
1. **Recommended for Cap:** first transition `Open → InProgress` **or** first assignee change + note, whichever comes first.
2. Alternative: dedicated `RespondedAt` set by API action “Mark responded”.

---

## Service layer contracts (Application)

```csharp
public interface ISlaPolicyService
{
    Task<IReadOnlyList<SlaPolicyDto>> ListAsync(...);
    Task<Result<SlaPolicyDto>> UpsertAsync(SlaPolicyUpsertDto dto, ...);
    Task<Result> SetDefaultAsync(int policyId, ...);
}

public interface ISlaClockService
{
    Task OnTicketCreatedAsync(ServiceTicket ticket, CancellationToken ct);
    Task OnTicketStatusChangedAsync(ServiceTicket ticket, ServiceTicketStatus from, ServiceTicketStatus to, CancellationToken ct);
    Task<SlaTicketSummaryDto?> GetTicketSlaAsync(int ticketId, CancellationToken ct);
}

public interface ISlaMonitorService
{
    Task<int> SweepAsync(CancellationToken ct); // returns events raised
}
```

**Integration point:** call clock hooks from `ServiceTicketService.CreateTicketAsync` / `ChangeStatusAsync` (same pattern as CRM notifications). Keep SLA logic **out of** the controller.

**Permissions (proposed):**
- `service.view` — see SLA strip on tickets
- `service.manage` — edit policies
- Optional: `service.sla.admin`

**Module:** stay under `service.tickets` / existing Service module; add nav “SLA Policies” under Service.

---

## W0 — Domain + policy foundation

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Entities: `SlaPolicy`, `SlaTarget` (+ enums) | Done |
| 2 | Migration + DbSets + company filter | Done |
| 3 | Seed one default policy (targets by priority) | Done |
| 4 | `ISlaPolicyService` + API list/upsert/set-default | Done |
| 5 | Permissions + tests (company isolation) | Done |

### Suggested default targets (parts counter — tune later)

| Priority | First response | Resolution |
|----------|----------------|------------|
| Urgent | 30 min | 4 h |
| High | 2 h | 1 business day |
| Normal | 4 h | 3 business days |
| Low | 1 business day | 5 business days |

**DoD:** Admin can CRUD a policy; tickets unchanged behaviorally yet.

---

## W1 — Clock engine on ticket lifecycle

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | `SlaTimer` + `SlaEvent` | Done |
| 2 | On create → start FR + Resolution timers | Done |
| 3 | On Open→InProgress → complete FR | Done |
| 4 | On Resolved/Closed → complete Resolution | Done |
| 5 | Elapsed = wall or simple business-hours stub | Done |
| 6 | `GET /api/service/tickets/{id}/sla` summary DTO | Done |
| 7 | Unit tests: met vs breach on status change | Done |

**DoD:** Creating a High ticket creates two Running timers; resolving within target → `Met`.

---

## W2 — Warn / breach / notify

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | `SlaMonitorBackgroundService` (poll 1–5 min) | Done |
| 2 | Warn at % → `INotificationService` to assignee (+ creator) | Done |
| 3 | Breach → sticky `Breached` + notify | Done |
| 4 | API filter: `slaStatus=warning|breached` | Done |
| 5 | Idempotent warn/breach (no spam every sweep) | Done |

**DoD:** Overdue timer without resolve flips to Breached once and notifies once.

---

## W3 — Escalation + pause reasons

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Pause when status Waiting / explicit pause reason | Done |
| 2 | Resume restores Running; elapsed excludes pause | Done |
| 3 | Escalation rule: on breach notify role/manager user id | Done |
| 4 | Optional auto-reassign stub (settings) | Done |
| 5 | Audit `SlaEvent` visible on ticket detail | Done |

**Pause map (recommended)**
- `Open`, `InProgress` → clocks run (FR until responded)
- Add `WaitingOnCustomer` / `WaitingOnParts` **or** use notes + pause API if you refuse new statuses initially
- `Resolved` / `Closed` → stop

If you want zero new statuses in W3: add `POST .../sla/pause|resume` with reason enum.

---

## W4 — UI

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | `/service/sla` policies editor (targets grid) | Done |
| 2 | Ticket detail: SLA strip (FR / Res remaining, badge) | Done |
| 3 | Tickets list: badge Ok / Warn / Breach | Done |
| 4 | Breach queue page or filter chip | Done |
| 5 | Mobile `/m/service`: compact SLA remaining | Done |
| 6 | Update PRODUCT-POSITIONING: “SLA timers (light)” claim | Done |

**UX rule:** One strip, not a dashboard of cards. Show remaining time + state only.

---

## W5 — Metrics, calendar polish, smoke

### Checklist

| # | Item | Status |
|---|------|--------|
| 1 | Business calendar (timezone + weekday hours) | Done |
| 2 | Dashboard: % met FR, % met Resolution, open breaches | Done |
| 3 | Smoke: create → warn path → resolve → Met | Done |
| 4 | MASTER + CHANGELOG + GUIDE-BUSINESS honest limits | Done |
| 5 | Performance: index `(CompanyId, Status)` on `SlaTimer` | Done |

---

## API sketch

```text
GET    /api/service/sla/policies
POST   /api/service/sla/policies
PUT    /api/service/sla/policies/{id}
POST   /api/service/sla/policies/{id}/default

GET    /api/service/tickets/{id}/sla
POST   /api/service/tickets/{id}/sla/pause
POST   /api/service/tickets/{id}/sla/resume

GET    /api/service/tickets?slaStatus=breached
GET    /api/service/sla/dashboard   (W5)
```

---

## Test matrix (must-have)

1. Company A policy not visible to Company B
2. Default policy attaches on create
3. FR Met when moved to InProgress before target
4. FR Breached when sweep runs past target still Open
5. Pause freezes elapsed; resume continues
6. Resolve after breach stays Breached historically but ticket closes
7. Warn fires once at 80%
8. Sales/service.view can read strip; only manage edits policies

---

## Sequencing vs other C2–C7 work

Ship **W0–W2** before knowledge base / portal / field service. Those depend on trustable ticket timing; SLA does not depend on them.

**Estimated slice size (for planning):** W0–W1 ≈ one focused PR; W2–W3 ≈ second; W4–W5 ≈ third + docs.

---

## Key files to touch (when implementing)

| Layer | Files |
|-------|--------|
| Domain | `ServiceEntities.cs` or new `SlaEntities.cs`, enums in `DomainEnums.cs` |
| Application | `SlaPolicyService`, `SlaClockService`, `SlaMonitorService`; hook `ServiceTicketService` |
| Infrastructure | Migration, `SlaMonitorBackgroundService`, DI |
| Api | extend `ServiceController` or `SlaController` |
| Web | `/service/sla`, `TicketDetail.razor`, `Tickets.razor`, `MobileService.razor` |
| Docs | this loop, MASTER Phase 8, PRODUCT-POSITIONING |
| Tests | `SlaClockTests`, `SlaMonitorTests`, company filter |

---

*Shipped 2026-08-07 — Program C2 SLA W0–W5 (policies, clocks, monitor, pause, UI, business calendar, dashboard).*
