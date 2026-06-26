# the-ledger — Architecture Decision Records

Append-only. ADRs are numbered sequentially and never renumbered; a reversal adds a
new ADR that supersedes the old one. Choices already mandated by the enterprise
guardrails (Postgres, EF Core, OTel, Terraform, GitHub Actions, MAF, shadcn) are
constraints, not decisions, and have no ADR.

## ADR-0001: Deploy on Azure Container Apps with scale-to-zero

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

The owner wants a serverless system that "spins up based on need" to keep idle cost near zero, while keeping the full .NET 10 + Aspire app model (a web API + a background worker + an SPA). Affects ARCH.md *Cloud topology* and *Containers*.

### Decision

We will deploy the API and Worker to **Azure Container Apps** with **scale-to-zero** (min replicas 0, KEDA-driven scale-out). Aspire's deployment target maps cleanly to ACA.

### Consequences

- **Positive**: near-zero idle cost; keeps the container/Aspire model (no FaaS rewrite); horizontal autoscale on HTTP/queue depth.
- **Negative**: **cold starts** on first request after idle — mitigated by an optional min-replica=1 on the API for paid tenants, and by the activation metric being upload-driven (the worker can warm asynchronously).
- **Neutral**: ties deployment to ACA specifics, isolated behind Terraform + `Infrastructure.Azure`.

### Alternatives considered

- **Azure App Service (Linux)** — simplest .NET host. Rejected: always-on billing conflicts with scale-to-zero intent.
- **Azure Functions** — finest-grained FaaS. Rejected: awkward fit for a full SPA-serving web API + Aspire; better suited only to the isolated parse worker (which we instead model as an ACA KEDA job, ADR-0005).

## ADR-0002: PDF-first ingestion via AI extraction, not bank aggregation

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

Mexico has no practically-available open banking (Ley Fintech secondary rules largely unissued through 2026); live aggregation requires a paid B2B provider (Belvo/Finerio). The product's primary on-ramp must work without bank APIs. Affects ARCH.md *Containers*, *MAF agents*, and the Ingestion epic.

### Decision

We will make **bank-statement PDF/CSV/manual ingestion** the v1 data source, using an **AI-extraction-first** pipeline (Azure OpenAI via the `StatementExtractionAgent`) with per-bank format hints and a balance-reconciliation validation pass. Live aggregation (Belvo) is a deferred, optional connector.

### Consequences

- **Positive**: works today in Mexico with zero bank partnerships; the differentiating wedge; user stays in control of their data.
- **Negative**: extraction can be imperfect → requires a human review-and-confirm queue and the >=95% accuracy metric; per-bank format drift is ongoing maintenance.
- **Neutral**: statement PDFs may contain card numbers → mandatory PAN redaction at ingestion (keeps PCI scope minimal).

### Alternatives considered

- **Belvo/Finerio aggregation** — live data. Rejected for v1: paid, B2B contracting, consent/regulatory weight.
- **Deterministic per-bank templates only** — cheap, no LLM. Rejected as primary: brittle across layout changes and digital-bank variety; kept as a fast-path for clean CSV.

## ADR-0003: Shared-database multi-tenancy with row-level tenant isolation

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

Multi-tenant SaaS for hundreds-to-low-thousands of households on a scale-to-zero budget. Need strong isolation without per-tenant infrastructure cost. Affects ARCH.md *Cross-cutting wiring* and *Data model*.

### Decision

We will use a **single PostgreSQL database** with a `TenantId` column on every tenant-owned entity, enforced by **EF Core global query filters** keyed on a scoped `ITenantContext` resolved from the auth token. Every write stamps `TenantId`; cross-tenant reads are impossible without explicitly ignoring the filter (operator support tooling only, audited).

### Consequences

- **Positive**: lowest cost/ops for the target scale; one migration path; fits scale-to-zero.
- **Negative**: a missing filter is a cross-tenant leak → enforced centrally in the DbContext + covered by an integration test that asserts isolation.
- **Neutral**: a future large tenant could be promoted to its own DB behind the same interface.

### Alternatives considered

- **Database-per-tenant** — strongest isolation. Rejected: cost + migration fan-out at this scale.
- **Schema-per-tenant** — middle ground. Rejected: provisioning + migration complexity without enough benefit for v1.

## ADR-0004: LLM-forward categorization via MAF with PII redaction and a rules fast-path

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

Mexican-Spanish merchant strings are messy (`OXXO`, `MERPAGO*`, `CFE`, `PAYPAL *SPOTIFY`). The owner chose an LLM-forward approach prioritizing accuracy. Sending transaction text to a hosted model exports potentially personal data. Affects ARCH.md *MAF agents* and the Ledger/AI-categorization epics.

### Decision

We will categorize transactions with a **rules fast-path first** (cheap, high-confidence, learned from corrections), then a **`CategorizationAgent` (MAF + Azure OpenAI)** for the remainder. **PII is redacted** (names, account numbers, CLABE) before any external call; a per-user opt-out falls back to rules-only. Corrections persist as `CategorizationRule` rows to shrink future model calls.

### Consequences

- **Positive**: high accuracy on local merchants; cost capped by the rules layer; improves over time.
- **Negative**: external model dependency + per-call latency/cost → handled async in the Worker via the outbox, never inline.
- **Neutral**: redaction + opt-out satisfy the LFPDPPP/GDPR posture in SPEC.

### Alternatives considered

- **Rules-only / on-prem** — max privacy. Rejected as default: lower accuracy on messy strings (offered as opt-out).
- **Azure Document Intelligence custom model** — structured extraction. Rejected for categorization: better fit for layout extraction than free-text merchant classification.

## ADR-0005: Statement parsing and outbox dispatch run in a queue-scaled worker

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

Parsing a PDF + LLM extraction is long-running and bursty; it must not block API requests, and must scale to zero when idle. The guardrails require an outbox for external side effects. Affects ARCH.md *Containers* and *Background work*.

### Decision

We will run a dedicated **`TheLedger.Worker`** Container App scaled by **KEDA on Azure Storage Queue** depth (scale-to-zero). The API enqueues a parse job (and outbox messages); the Worker drains the queue to parse statements, call the LLM, and send email. Scheduled jobs (nightly recurring sweep, daily net-worth snapshot, bill reminders) run on the Worker's single in-process scheduler.

### Consequences

- **Positive**: API stays fast and stateless; parse load scales independently from zero; outbox guarantees external side effects survive restarts.
- **Negative**: eventual consistency between upload and "transactions ready" → surfaced in the UI as a parsing status.
- **Neutral**: queue + worker is one more moving part, justified by the workload shape.

### Alternatives considered

- **Synchronous parse in the API request** — simplest. Rejected: long requests, cold-start amplification, no retry isolation.
- **Azure Functions for the parse** — FaaS. Rejected: keep one runtime/observability story; ACA KEDA job gives the same scale-to-zero.

## ADR-0006: Entra External ID (CIAM) for authentication

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

Multi-tenant consumer signup needs email/password + social, must never store raw passwords, and should integrate with Azure + OIDC per the guardrails. Affects ARCH.md *Cross-cutting wiring* and *Cloud topology*.

### Decision

We will use **Entra External ID** (Microsoft's CIAM) as the OIDC identity provider. The SPA uses MSAL (auth code + PKCE); the API validates JWT bearer tokens; the household/`tenantId` is carried as a token claim.

### Consequences

- **Positive**: no password storage; managed MFA/social; first-class on Azure + the guardrail's OIDC mandate; free/low tier for v1 volumes.
- **Negative**: tenant-claim mapping (user → household) must be provisioned at signup → handled in the Foundations onboarding flow.
- **Neutral**: swapping IdPs later is bounded (OIDC behind config).

### Alternatives considered

- **Self-managed ASP.NET Core Identity** — full control. Rejected: we own password storage + reset + MFA, more security surface.
- **Auth0 / Clerk** — strong DX. Rejected: extra vendor + cost when Entra External ID covers it on-cloud.

## ADR-0007: Ship the SPA as an installable PWA

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

"Usable from a mobile" is a hard requirement; primary flows must work one-handed on a phone, including on-the-go statement upload. Native apps are out of v1 scope. Affects ARCH.md *SPA architecture* and feature #10.

### Decision

We will build the SPA **mobile-first** and ship it as an **installable PWA** (`vite-plugin-pwa`): home-screen install, app-like standalone display, offline-light shell, and device file/camera access for statement upload.

### Consequences

- **Positive**: app-like mobile experience with one codebase; no app-store friction; meets the mobile completion metric.
- **Negative**: PWA platform limits (e.g. iOS background) → acceptable; no v1 feature needs native background.
- **Neutral**: service-worker caching must be versioned carefully to avoid stale assets.

### Alternatives considered

- **Responsive web only (no PWA)** — simpler. Rejected: loses installability/home-screen + offline shell that make it feel like an app.
- **Native (MAUI / RN)** — best mobile fidelity. Rejected: out of v1 scope, doubles the surface.

## ADR-0008: No vector store in v1 (defer pgvector)

- **Status**: accepted
- **Date**: 2026-06-24
- **Deciders**: Architecture (Stage 2)

### Context

The guardrail default for a vector store is Postgres + pgvector, but v1 has no semantic-search/RAG requirement — categorization is direct LLM classification and merchant memory is a deterministic rules table. Affects ARCH.md *Cloud topology*.

### Decision

We will **not provision a vector store** in v1. Merchant→category memory lives in `CategorizationRule` (exact/normalized match). pgvector is deferred until a feature (e.g. semantic merchant similarity or a finance Q&A over documents) needs it.

### Consequences

- **Positive**: less infrastructure, lower cost, simpler ops for v1.
- **Negative**: fuzzy merchant matching relies on normalization + LLM rather than embeddings → acceptable given the rules + LLM design.
- **Neutral**: adding pgvector later is a single extension on the existing Postgres, no new service.

### Alternatives considered

- **Provision pgvector now** — guardrail default. Rejected: no v1 consumer; YAGNI.
- **External vector DB (Pinecone/Azure AI Search)** — managed. Rejected: unnecessary cost + vendor for a non-existent v1 need.

## ADR-0009: Receipt/ticket OCR via Azure Document Intelligence `prebuilt-receipt`

- **Status**: accepted
- **Date**: 2026-06-26
- **Deciders**: Architecture (Stage 2)

### Context

Epic 9 (v1.1) lets a user snap a photo of a Mexican store *ticket*/receipt and have it become a transaction. This is **layout/structured extraction** from a photo (merchant, date, total, tax, line items), not free-text classification. ADR-0004 already noted Document Intelligence is the right tool for layout extraction (and the wrong one for merchant categorization). Affects ARCH.md *Containers*, *MAF agents*, and feature #49. The capability is declared in `workflow.json` as `document-intelligence` (provider `azure-document-intelligence`).

### Decision

We will use **Azure AI Document Intelligence** with the **`prebuilt-receipt`** model for receipt/ticket OCR, behind an `IReceiptExtractor` interface in `Application.Ingestion` with the Azure implementation in `Infrastructure.Azure`. OCR runs in the Worker via the outbox; a deterministic fake `IReceiptExtractor` backs dev/tests so CI needs no Azure dependency. The structured result is normalized (merchant string + category) by the `ReceiptNormalizationAgent` reusing `ICategorizer`, then lands as a *staged* transaction in the existing review-and-confirm queue.

### Consequences

- **Positive**: a managed, pre-trained receipt model — no training data or custom-model lifecycle; high-quality field extraction including line items + tax; keeps the LLM for what it's good at (merchant normalization, categorization).
- **Negative**: a second Azure AI dependency + per-page cost → bounded by user-initiated capture and the outbox/worker async path; another Key Vault secret + Managed Identity grant.
- **Neutral**: receipts can contain a partial card PAN → the same masking/redaction as statement ingestion applies before persistence (ADR-0002).

### Alternatives considered

- **General OCR + LLM parse (Azure OpenAI vision / `prebuilt-read` + prompt)** — fewer services. Rejected as primary: lower structured accuracy on receipts (totals/line items/tax) than the purpose-built receipt model; reserved as a fallback if a receipt fails the receipt model.
- **Custom Document Intelligence model trained per Mexican chain** — highest accuracy on specific tickets. Rejected for v1.1: training-data + model-lifecycle cost; `prebuilt-receipt` already handles Spanish/MXN receipts; revisit only if a specific high-volume chain underperforms.
- **Tesseract / open-source OCR self-hosted** — no per-call cost. Rejected: worse accuracy, no structured receipt schema, more ops on a scale-to-zero budget.

## ADR-0010: WhatsApp capture via the Meta WhatsApp Business Cloud API behind the connector contract

- **Status**: accepted
- **Date**: 2026-06-26
- **Deciders**: Architecture (Stage 2)

### Context

Epic 9 (v1.1) adds a WhatsApp number that ingests a receipt photo or a natural-language message into a staged transaction and pushes alerts outbound. WhatsApp is the dominant messaging channel in Mexico, so it's the highest-leverage "introduce data easily" surface. It must fit the existing `pluggable-connectors` registry and the outbox. Affects ARCH.md *Containers*, *Cross-cutting wiring*, *API surface*, and feature #50. Declared in `workflow.json` as the `whatsapp` connector.

### Decision

We will integrate WhatsApp via the **Meta WhatsApp Business Cloud API** directly, implemented as `Infrastructure.Connectors.WhatsApp` and registered through the existing `ConnectorRegistry` as an `IChannel`. Inbound is a webhook: `GET /connectors/whatsapp/webhook` answers the subscription verify challenge; `POST` validates the `X-Hub-Signature-256` HMAC of the raw body against the app secret (Key Vault) before processing, dedupes on the WhatsApp message id, resolves the sender phone to an opted-in `User`, and routes media → `IReceiptExtractor` / text → `QuickAddParserAgent`. Outbound send is an `IWhatsAppSender` driven through the outbox; a fake sender backs dev/tests. Unknown senders get a generic help reply — never tenant data.

### Consequences

- **Positive**: first-party, well-documented webhook + send API; no extra reseller margin; HMAC-verified inbound; reuses the connector registry, outbox, OCR, and NL-parse already built — small net-new surface.
- **Negative**: Meta app review + a WhatsApp Business Account/number provisioning step (ops, not code); 24-hour customer-service-window rules for free-form outbound → bill/anomaly alerts outside the window must use pre-approved message templates.
- **Neutral**: the `IChannel`/`IWhatsAppSender` abstraction keeps the provider swappable (e.g. to ACS Advanced Messaging or Twilio) without touching handlers.

### Alternatives considered

- **Azure Communication Services Advanced Messaging (WhatsApp)** — Azure-native, consistent with the ACS email connector, Managed Identity. Rejected as the v1.1 default: inbound arrives via Event Grid (more moving parts than a single verified webhook) and the WhatsApp channel is newer/less battle-tested; kept as the most likely future swap given we already use ACS.
- **Twilio WhatsApp** — excellent DX, fast onboarding. Rejected: an extra vendor + per-message reseller cost when the first-party Cloud API is free-tier and direct.
- **No WhatsApp; web/PWA capture only** — least surface. Rejected: WhatsApp is the explicit ask and the highest-adoption capture channel for the Mexican market.

## ADR-0011: Natural-language quick-add via the existing chat client, structured output, confirm-before-persist

- **Status**: accepted
- **Date**: 2026-06-26
- **Deciders**: Architecture (Stage 2)

### Context

Epic 9 (v1.1) lets a user type or dictate "comí 350 en restaurante ayer" and get a transaction draft, in the SPA and via inbound WhatsApp text. This is free-text → structured intent, the LLM's strength, and the same Azure OpenAI client already wired for categorization. Affects ARCH.md *MAF agents*, *API surface*, and feature #51. The risk is a wrong parse silently writing a bad transaction.

### Decision

We will implement an `INaturalLanguageTransactionParser` (the `QuickAddParserAgent`, MAF + the existing Azure OpenAI `IChatClient`) returning a **schema-validated** draft `{amount, currency, date, direction, merchant, categoryId, confidence}`, with dates resolved relative to *today* in **America/Mexico_City**. The draft is **always surfaced for explicit user confirmation** before it persists — no silent writes, on either the web or WhatsApp path. PII is redacted before the call; the feature is gated on the existing LLM opt-in consent; a deterministic fake parser backs dev/tests.

### Consequences

- **Positive**: the single lowest-friction data-entry path; reuses the existing model wiring and redaction; one parser shared by the SPA quick-add bar and WhatsApp text.
- **Negative**: LLM parse can be wrong (amount/date/direction) → mitigated by mandatory confirm + a visible confidence + easy edit; relative-date resolution must pin the MX timezone to avoid off-by-one.
- **Neutral**: structured output keeps the contract strongly typed and server-validated; a low-confidence draft simply pre-fills the normal add-transaction form.

### Alternatives considered

- **Deterministic regex/grammar parser** — no model cost, fully predictable. Rejected as primary: brittle across Spanish phrasing, slang, and relative dates; kept as a cheap pre-pass for trivial "<amount> <merchant>" forms.
- **Auto-post without confirmation when confidence is high** — fewer taps. Rejected: a wrong auto-posted transaction erodes trust in a finance app; confirmation is cheap and the safety win is large.
