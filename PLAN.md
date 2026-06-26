# the-ledger — Plan

> Planned for v1's success metrics at the smallest scale that meets them
> (hundreds-to-low-thousands of tenants), not 10×. Concrete technology, schemas,
> and C4 diagrams are deferred to `/architecture:design-architecture`. Build order
> is fixed: Foundations first, capabilities next, differentiators last so they can
> slip without blocking v1.

## Epics (in build order)

1. **Foundations** — auth (OIDC), multi-tenancy (tenant id on every domain table, EF query filters, resolved tenant context per request), observability (OpenTelemetry via ServiceDefaults, health checks), append-only audit logging, RBAC scaffold (policies in config), API conventions (versioning, Problem Details, idempotency keys, per-tenant rate limiting), distributed cache, single in-process background scheduler + **outbox**, connector registry, GDPR/ARCO **data-export + per-tenant delete** machinery with `[Pii]` tagging, and the **mobile-first responsive dashboard shell** (installable, app-like on phones) with the assistant panel. Always epic 1; pulled from the enterprise guardrails. Capabilities (from SPEC): *Multi-tenant household accounts*, *Mobile-first responsive experience*. Depends on: nothing.
2. **Ingestion & Statements** — upload a Mexican bank-statement **PDF**, CSV, or enter manually; an AI-extraction-first parse pipeline with per-bank format hints and a balance-reconciliation validation pass; a review-and-confirm queue; **card-number masking at ingestion**. Capabilities (from SPEC): *Statement & transaction ingestion*; differentiator *Mexican bank-statement PDF parsing* (BBVA, Santander, Banorte, Nu/Hey/Klar). Depends on: Foundations.
3. **Ledger & Categorization** — accounts (checking/savings/card/cash), the unified categorized transaction feed, edit/split/recategorize, system + custom categories, and **rule-based auto-categorization** that learns from corrections. Capabilities (from SPEC): *Unified accounts & transaction ledger*. Depends on: Foundations, Ingestion (transactions originate from ingestion).
4. **Budgets & Goals** — flexible monthly **category-target budgets** with spent-vs-target and rollover; named **savings goals** with progress. Capabilities (from SPEC): *Flexible category budgets*, and the *goals* slice of *Net worth, goals & spending insights*. Depends on: Ledger.
5. **Insights & Net Worth** — assets−liabilities **net worth over time**, spending **trends/reports** by category and month, and **CSV/data export**. Capabilities (from SPEC): the *net-worth + insights* slice of *Net worth, goals & spending insights*. Depends on: Ledger.
6. **Alerts & Recurring** — **recurring-transaction detection** (salary, subscriptions), **upcoming-bill reminders**, and **anomaly alerts** (duplicate charge, new fee, low balance, unusual spend), delivered **in-app + email**. Capabilities (from SPEC): *Recurring detection, bills & anomaly alerts*. Depends on: Ledger; uses Foundations outbox + email connector.
7. **AI Categorization** *(differentiator)* — **LLM-forward** categorization tuned for Mexican-Spanish merchant strings, with **PII redaction before any external model call**, a rules fast-path to cap cost, and learning from user corrections. Capabilities (from SPEC): differentiator *AI-assisted categorization for Mexican-Spanish merchants*. Depends on: Ledger & Categorization (replaces/augments the rules categorizer behind the same interface).
8. **Shared Household** *(differentiator)* — the collaborative layer on top of Foundations' tenancy: **joint budgets**, **per-member transaction attribution**, **shared goals**, and a shared household view with separate logins. Capabilities (from SPEC): differentiator *Shared household finances*. Depends on: Foundations (tenancy/roles), Budgets & Goals.
9. **AI Capture & Channels** *(differentiator — added v1.1)* — the effortless-data-entry layer so a user introduces data without a keyboard. Three capture surfaces, all landing in the existing review-and-confirm queue (epic 2) with card-masking + PII redaction before persistence, and all external OCR/LLM calls routed through the **outbox**: (a) **Receipt/ticket scanning** — snap a photo of a Mexican store *ticket* and have **Azure Document Intelligence** (`prebuilt-receipt`) OCR it into a staged transaction (merchant, date, total, line items, tax), with the LLM normalizing merchant strings; (b) **WhatsApp capture & alerts** — a WhatsApp Business number that accepts a receipt photo *or* a natural-language message ("gasté 200 en el Oxxo") and turns it into a staged transaction, plus outbound bill/anomaly/export-ready notifications over the same channel; (c) **AI natural-language quick-add** — type/dictate "comí 350 en restaurante ayer" anywhere in the SPA and the LLM parses it to a transaction draft. Capabilities (new in v1.1): *Receipt/ticket scanning (Azure Document Intelligence)*, *WhatsApp capture & alerts*, *AI natural-language quick-add*. Depends on: Ingestion (review queue), Ledger (transactions + categorization), Alerts (outbound channel), Foundations (connector registry, outbox, consent — explicit LLM/third-party opt-in per user).

Every must-have and differentiator capability in SPEC (plus the v1.1 capture additions in epic 9) appears in exactly one epic above; none invented, none dropped.

## Module list

| Module (.NET project) | Bounded context | Capabilities served | Skills used to build it |
|---|---|---|---|
| `TheLedger.AppHost` | (orchestration) | cross-cutting | aspire, dotnet-aspire-base |
| `TheLedger.ServiceDefaults` | (observability/resilience) | cross-cutting | dotnet-aspire-base, aspire |
| `TheLedger.Domain` | (all contexts' entities) | all | entity-framework-core |
| `TheLedger.Application.Foundations` | foundations | Multi-tenant household accounts; audit; GDPR/ARCO export+delete; RBAC | dotnet-aspire-base, entity-framework-core |
| `TheLedger.Application.Ingestion` | ingestion | Statement & transaction ingestion; MX PDF parsing; receipt/ticket OCR (Document Intelligence); NL quick-add | agent-framework-csharp, anthropic-skills:pdf, entity-framework-core |
| `TheLedger.Infrastructure.Azure` | (azure adapters) | Blob storage; Azure OpenAI; **Azure Document Intelligence** `prebuilt-receipt` adapter | aspire, entity-framework-core |
| `TheLedger.Infrastructure` (connectors) | (channel adapters) | email; **WhatsApp** connector (inbound webhook + outbound send) | pluggable-connectors |
| `TheLedger.Application.Ledger` | ledger | Unified accounts & ledger; auto-categorization | entity-framework-core, agent-framework-csharp |
| `TheLedger.Application.Budgeting` | budgeting | Flexible category budgets; goals | entity-framework-core |
| `TheLedger.Application.Insights` | insights | Net worth; spending insights; reports/export | entity-framework-core |
| `TheLedger.Application.Alerts` | alerts | Recurring detection; bills; anomaly alerts | entity-framework-core, pluggable-connectors |
| `TheLedger.Api` | (HTTP surface, all contexts) | versioned endpoints; RBAC policies; idempotency; rate limiting | dotnet-aspire-base, aspire |
| `TheLedger.Infrastructure` | (persistence/identity/adapters) | EF Core, identity, secrets, parse + AI adapters, email/outbox | entity-framework-core, agent-framework-csharp, pluggable-connectors |
| `TheLedger.Web` | (mobile-first SPA) | Mobile-first responsive UI across all capabilities | react-vite-shadcn |

> AI categorization (epic 7) does not get its own bounded context — it's an
> `ICategorizer` implementation in `TheLedger.Infrastructure` (a MAF agent +
> redaction step) swapped behind the Ledger context's categorization interface, so
> the rules path and the LLM path are interchangeable.

## Data model sketch

Conceptual entities and relationships only (schemas are `/architecture:design-architecture`'s job). Every entity except system-default `Category` rows carries a `tenantId` and is scoped by EF query filters. `[Pii]` marks fields that flow through audit logging and data export.

- **Tenant (Household)** — `id`, `name`, `plan`, `createdAt`. The multi-tenancy root; everything below hangs off it.
- **User** — `id`, `tenantId`, `email`[Pii], `displayName`[Pii], `role` (owner/member/viewer), `externalAuthId`. Belongs to one household (v1).
- **Account** — `id`, `tenantId`, `name`, `type` (checking/savings/card/cash), `institution`, `currency`, `maskedNumber`[Pii-masked], `currentBalance`.
- **Statement** — `id`, `tenantId`, `accountId`, `sourceType` (pdf/csv/manual), `fileRef`[Pii], `period`, `status` (uploaded→parsing→parsed→confirmed), `uploadedByUserId`. Raw file stored encrypted, card numbers redacted before persistence.
- **Transaction** — `id`, `tenantId`, `accountId`, `statementId?`, `date`, `description`[Pii], `amount`, `currency`, `direction` (debit/credit), `categoryId`, `attributedUserId?` (per-member, epic 8), `isRecurring`, `categorizationSource` (rule/llm/manual), `confidence`.
- **Category** — `id`, `tenantId?` (null = system default), `name`, `parentId?`, `kind` (income/expense).
- **CategorizationRule** — `id`, `tenantId`, `matchPattern`, `categoryId`, `priority`. Learned from corrections.
- **Budget** — `id`, `tenantId`, `categoryId`, `periodMonth`, `targetAmount`, `rollover`.
- **Goal** — `id`, `tenantId`, `name`, `targetAmount`, `currentAmount`, `targetDate?`, `linkedAccountId?`.
- **RecurringSeries** — `id`, `tenantId`, `merchant`, `cadence`, `expectedAmount`, `nextExpectedDate`.
- **Alert** — `id`, `tenantId`, `type` (duplicate/new-fee/low-balance/unusual/bill-due), `transactionId?`, `status` (new/seen/dismissed), `createdAt`.
- **ConsentRecord** — `id`, `tenantId`, `userId`, `type` (aviso-privacidad/llm-opt-in), `version`, `grantedAt`. LFPDPPP/GDPR lawful-basis evidence.
- **AuditEntry** — `id`, `tenantId`, `userId`, `action`, `entityType`, `entityId`, `before`, `after`, `timestamp`. Append-only, stored outside the operational DB.
- **OutboxMessage** — `id`, `tenantId`, `type`, `payload`, `status`, `attempts`. Drives all external side effects (email, LLM calls).

## RBAC model (refined)

Policy names use `<Module>.<Action>`. Code references the policy, not the role. Roles are config-bound.

| Role | Policies | Notes |
|---|---|---|
| `owner` | `Households.Manage`, `Members.Invite`, `Members.Manage`, `Accounts.Manage`, `Statements.Upload`, `Statements.Delete`, `Transactions.Edit`, `Budgets.Manage`, `Goals.Manage`, `Alerts.Manage`, `Insights.View`, `Data.Export`, `Data.Delete`, `Billing.Manage` (+ all `.View`) | Full control of their own tenant only. |
| `member` | `Accounts.View`, `Statements.Upload`, `Transactions.View`, `Transactions.Edit`, `Budgets.View`, `Budgets.Edit`, `Goals.View`, `Goals.Edit`, `Insights.View`, `Alerts.View` | Participates; cannot manage members, billing, or delete the household. |
| `viewer` | `Accounts.View`, `Transactions.View`, `Budgets.View`, `Goals.View`, `Insights.View`, `Alerts.View` | Read-only across the household. |
| `operator` | `Tenants.Provision`, `Instance.Monitor`, `Parsers.Configure`, `Categorization.Configure`, `DataSubject.Execute` | Platform admin. **No tenant financial-data read** except via explicitly audited support tooling. |

## Integration surface

Connectors are declared in `workflow.json` during compose (Phase 6); this is the planned surface.

| Connector | Direction | Purpose | Webhook routes | Per-tenant config |
|---|---|---|---|---|
| `email` | outbound | Member invitations + alert/bill/export-ready notifications | — (outbound only) | sender display name, reply-to |
| `whatsapp` *(v1.1)* | inbound + outbound | Receipt-photo & NL-text capture → staged transactions; outbound bill/anomaly/export-ready alerts | `/api/v1/connectors/whatsapp/webhook` (GET verify + POST receive) | business phone-number id, app secret (HMAC verify), per-user opt-in |
| `belvo` *(deferred, post-v1)* | inbound | Live Mexican bank aggregation as an optional paid connector | `/api/v1/connectors/belvo/webhook/...` | institution links, consent token (per user) |

> Statement upload is a first-party user action, not a connector. Belvo is listed
> for traceability but is **out of v1 scope** (SPEC out-of-scope).

## Background work

Single in-process scheduler (guardrail). External side effects go through the **outbox** — never fire-and-forget from a handler.

| Job | Trigger | Cadence | Outbox required? |
|---|---|---|---|
| Statement parse (extract → validate → stage transactions) | reactive | on upload | no (internal stage) |
| AI categorization of new/low-confidence transactions | reactive | on transactions staged | **yes** (external LLM call) |
| Recurring-series detection | reactive + scheduled | on new transactions; nightly sweep | no |
| Anomaly/alert evaluation | reactive | on new transactions | no (alert row); **yes** to send the email |
| Bill-due reminders | scheduled | daily | **yes** (email + WhatsApp) |
| Receipt OCR (Document Intelligence extract → stage transaction) | reactive | on receipt upload / WhatsApp media | **yes** (external OCR + LLM normalize) |
| WhatsApp inbound capture (media/NL-text → staged transaction) | reactive | on webhook POST | **yes** (OCR/LLM call) |
| WhatsApp outbound alert send | reactive | on alert raised | **yes** |
| Net-worth daily snapshot | scheduled | daily | no |
| GDPR/ARCO data export build | reactive | on request | **yes** (email "export ready") |
| Tenant data deletion | reactive | on request | no |

## Open questions for design-architecture

1. **Statement storage + parse compute on scale-to-zero.** Where do uploaded PDFs live (object storage) and what runs the parse so it scales from zero (queue + worker / Container Apps job)? This is the core serverless decision and drives the <5-min activation metric.
2. **Tenant resolution.** For a shared SaaS + self-host codebase, resolve `tenantId` from the auth token claim (not subdomain) and enforce via EF global query filters — confirm and pin the resolver.
3. **LLM-forward categorization runtime.** Which model/runtime (MAF + Azure OpenAI assumed) and exactly where the PII-redaction step sits relative to the model call; how corrections feed the rules fast-path.
4. **Identity provider.** Concrete OIDC provider (Entra External ID assumed) and how `operator` vs tenant roles are represented in tokens and resolved per request.
5. **Cold-start budget.** Acceptable cold-start latency for the API and parse worker on Container Apps, and the min-replica policy that balances cost (scale-to-zero) against the activation metric.
