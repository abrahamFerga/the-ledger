# the-ledger

A serverless, multi-tenant **personal finance** system — track accounts, parse
bank-statement PDFs, categorize transactions, and budget — built for Mexico-first
banking (no open-banking API dependency) and usable by anyone who self-hosts or
signs up.

> 🚧 **Status: bootstrapping.** This repository is being generated through a
> staged, GitHub-native build pipeline (research → spec → plan → backlog →
> architecture → development). The backlog of features lives in this repo's
> Issues and Projects board.

## Vision

- **PDF-first ingestion.** Mexican banks rarely expose open-banking APIs, so the
  primary on-ramp is uploading bank-statement PDFs and extracting transactions —
  with manual entry and CSV import as fallbacks.
- **Commercial-grade features.** Budgeting, categorization, recurring-transaction
  detection, net-worth tracking, and reports — drawing on patterns from Mint,
  YNAB, Monarch, Copilot, and Fintonic.
- **Serverless / scale-to-zero.** Runs on demand to keep idle cost near zero.
- **Multi-tenant.** Built for one household first, designed so others can use it too.

## Architecture

.NET 10 + Aspire backend, React + Vite + Tailwind (mobile-first PWA) frontend,
deployed to Azure Container Apps (scale-to-zero). See [ARCH.md](ARCH.md),
[DECISIONS.md](DECISIONS.md) (ADRs), and the C4 diagrams in `docs/diagrams/`.

## Repository layout

```
src/
  TheLedger.AppHost            Aspire orchestration (Postgres, Redis, API, Worker)
  TheLedger.ServiceDefaults    OpenTelemetry, health checks, resilience
  TheLedger.Api                Minimal APIs (/api/v1), auth, RBAC, idempotency, rate limiting
  TheLedger.Worker             Outbox dispatcher + scheduled jobs
  TheLedger.Domain             Entities + primitives ([Pii], ITenantOwned)
  TheLedger.Application[.*]    Use-case contracts (Foundations: households, data-subject)
  TheLedger.Infrastructure     EF Core DbContext, tenant query filters, audit interceptor
tests/                         Domain unit tests + tenant-isolation tests (SQLite, no Docker)
web/                           Vite + React + TS mobile-first PWA
```

## Local development

```bash
# Backend (needs Docker for the Aspire-managed Postgres + Redis)
dotnet run --project src/TheLedger.AppHost      # opens the Aspire dashboard

# Frontend
cd web && npm install && npm run dev            # http://localhost:5173

# Tests (no Docker required)
dotnet test
```

Without a configured OIDC authority the API runs a header-driven **Dev** auth scheme:
send `X-Dev-Tenant`, `X-Dev-User`, and `X-Dev-Role` to simulate a signed-in member.

## Status

**v1 complete — all 11 backlog features implemented and merged.** The solution builds
clean (`dotnet build -c Release`) with **26 passing tests**, and the
[project board](https://github.com/users/abrahamFerga/projects/2) is fully Done.

Implemented features:
1. Multi-tenant household accounts (auth, tenancy, RBAC, audit, GDPR/ARCO export+delete)
2. Mobile-first responsive PWA shell
3. Statement & transaction ingestion (PDF/CSV/manual + review queue + PAN masking)
4. Mexican bank-statement PDF parsing (heuristic extractor + balance reconciliation)
5. Unified accounts & transaction ledger (feed, edit/split/recategorize, rules engine)
6. Flexible category budgets (spent-vs-target + rollover)
7. Savings goals (contributions + account-linked progress)
8. Net worth, spending insights & CSV export
9. Recurring detection, bills & anomaly alerts
10. AI-assisted categorization (LLM-forward via MEAI `IChatClient`, PII-redacted)
11. Shared household finances (per-member attribution + member-spend breakdown)

### Known follow-ups (config / infra)
- Replace dev-time `EnsureCreated` with **EF Core migrations**.
- Wire a concrete `IChatClient` (Azure OpenAI) and the **email** connector (the outbox already enqueues).
- Move statement bytes to **Azure Blob**; add **Terraform + GitHub Actions** IaC (Azure Container Apps).
- Runtime verification harness (Aspire integration tests against real Postgres/Redis; Playwright E2E).

## License

MIT — see [LICENSE](LICENSE).
