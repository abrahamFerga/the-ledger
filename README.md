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

## Architecture (target)

.NET 10 + Aspire backend, React + Vite + shadcn/ui frontend, deployed to Azure
Container Apps (scale-to-zero). See `ARCH.md` once the architecture phase completes.

## License

MIT — see [LICENSE](LICENSE).
