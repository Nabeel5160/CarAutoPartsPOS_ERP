# ADR-003: CQRS via MediatR

## Status
Accepted

## Decision
New enterprise modules (platform, finance, and later P2P/O2C) use **MediatR** commands/queries. Legacy fat `I*Service` classes remain for existing POS/catalog paths and are migrated incrementally.

## Consequences
- Controllers for new modules send `IRequest` / `IRequest<T>`.
- Application stops growing unbounded `IQueryable` usage on new code paths.
