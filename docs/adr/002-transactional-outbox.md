# ADR-002: Transactional outbox

## Status
Accepted

## Decision
Side effects (FBR submission, GL auto-posting, emails) are written as `OutboxMessage` rows in the **same EF transaction** as the business change. A hosted `OutboxProcessor` polls and dispatches.

## Consequences
- At-least-once delivery; handlers must be idempotent.
- Avoids dual-write failures between SQL and external APIs.
