# ADR-001: Multi-company tenancy

## Status
Accepted

## Decision
Single SQL Server database with **multi-company** isolation via `CompanyId` on all company-owned entities (`ICompanyOwned`). One physical deployment serves multiple legal companies.

## Consequences
- Global EF query filters enforce company scope when `ICurrentCompanyContext` is set.
- JWT carries `company_id` claim; API also accepts `X-Company-Id` for admin switch (permission-gated).
- Existing rows are backfilled to a seeded default company on migration.
