# Requirements inbox

Raw, user-stated requirements captured during the build, before they're
formalized into `SPEC.md` (non-functional + capabilities) and `ARCH.md`. Each
gets a home in a later artifact; this file is the running source of truth until
then.

## Stated by the user

- **Domain:** a system to handle personal finances, fully working for the owner
  first, but usable by other people too.
- **Market context:** owner lives in **Mexico**. Bank open-banking APIs are
  likely unavailable, so **bank-statement PDF parsing** is a primary ingestion
  path (with manual entry / CSV import as fallbacks).
- **Inspiration:** draw ideas from commercial products — Mint, YNAB, Monarch,
  Copilot, Fintonic, and similar.
- **Deployment:** **serverless / scale-to-zero** — spin up based on need to keep
  idle cost near zero. Target: Azure Container Apps.
- **Tenancy:** **multi-tenant SaaS from day one** — per-user/household accounts
  with tenant isolation, sign-up flow.
- **Mobile:** must be usable from a mobile device — the UI must be **fully
  responsive, mobile-first**. Primary flows (check balances, review/categorize
  transactions, upload a statement) must work one-handed on a phone. Strong
  candidate for a **PWA** (installable, mobile statement upload).

## Disposition (filled in as artifacts are written)

| Requirement | Lands in |
|---|---|
| Personal-finance domain, multi-user | SPEC personas + jobs-to-be-done |
| Mexico-first, PDF ingestion | SPEC must-haves; research competitive matrix |
| Commercial-product inspiration | research/personal-finance.md |
| Serverless / scale-to-zero | ARCH (Azure Container Apps), DECISIONS ADR |
| Multi-tenant SaaS | SPEC RBAC + ARCH tenancy model |
| Mobile-first responsive + PWA | SPEC NFR; ARCH SPA section + ADR |
