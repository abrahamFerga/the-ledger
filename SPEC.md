# the-ledger — Product specification

## In one sentence

**the-ledger** turns Mexican bank-statement PDFs (plus CSV and manual entry) into
a categorized, budgeted, mobile-first view of your money — usable as a personal
app for one household or as a multi-tenant service others can sign up for, with no
dependency on bank APIs that don't exist in Mexico yet.

## Primary jobs to be done

- When my monthly statement arrives, I want to **upload the PDF and have my transactions extracted and categorized automatically**, so that I never type them in by hand.
- When I review my spending, I want to **see all my accounts and a categorized feed in one place**, so that I know where my money actually went.
- When I plan the month, I want to **set a target per category and track spending against it**, so that I catch overspending before it happens.
- When I'm out during the day, I want to **check a budget or log a cash expense one-handed on my phone**, so that I stay on top of money without opening a laptop.
- When my partner and I manage money together, I want us to **share one household ledger with separate logins**, so that we both see the full picture.
- When something looks wrong, I want to be **alerted to duplicate charges, new fees, or unusual spending**, so that I notice problems early.
- When I want to leave or audit the service, I want to **export or permanently delete all my data**, so that I stay in control.

## Target personas

- **Owner (household admin)** — the person who owns a household ledger and runs its money. Top 3 tasks:
  1. Upload a bank-statement PDF and confirm the extracted, categorized transactions.
  2. Set and adjust monthly category budgets.
  3. Invite a partner/family member and manage their access.
- **Member (partner / family)** — an invited participant in a shared household. Top 3 tasks:
  1. View the shared feed, net worth, and budget progress.
  2. Add or edit a manual/cash transaction and recategorize it.
  3. Check a category's remaining budget one-handed on a phone.
- **Operator (self-hoster / instance admin)** — runs an instance for themselves or for others. Top 3 tasks:
  1. Stand up / spin up the instance and onboard the first tenant.
  2. Configure which bank parsers and categorization behavior are enabled.
  3. Fulfill a data-subject request (export or delete a user's data) and check instance health.

## Capabilities

### Must have (v1)

| Capability | One-line description | Personas |
|---|---|---|
| Statement & transaction ingestion | Upload a Mexican bank-statement PDF, CSV, or enter transactions manually; extracted rows land in a review queue with card numbers masked. | Owner, Member |
| Unified accounts & transaction ledger | All accounts (checking, savings, card, cash) in one categorized feed; edit, split, recategorize, and rule-based auto-categorization out of the box. | Owner, Member, Viewer |
| Flexible category budgets | Monthly target per category with spent-vs-target tracking and rollover. | Owner, Member |
| Net worth, goals & spending insights | Assets − liabilities over time, named savings goals, and spending trends/reports by category and month; exportable. | Owner, Member, Viewer |
| Recurring detection, bills & anomaly alerts | Detect recurring income/subscriptions, surface upcoming bills, and alert on duplicate charges, new fees, low balance, or unusual spend. | Owner, Member |
| Multi-tenant household accounts | Sign-up, per-tenant data isolation, member invitations, and role-based access. | Owner, Member, Operator |
| Mobile-first responsive experience | Every primary flow (check balances, review & categorize, upload a statement) works one-handed on a phone; installable, app-like on mobile. | Owner, Member |

### Differentiators (v1)

| Capability | Why it matters | Personas |
|---|---|---|
| Mexican bank-statement PDF parsing | The wedge: no commercial PFM parses statements, and Mexico has no real open banking — "upload your PDF and it just works" is the headline. Targets BBVA, Santander, Banorte, and digital banks (Nu/Hey/Klar). | Owner, Member |
| AI-assisted categorization for Mexican-Spanish merchants | Messy local merchant strings (`OXXO`, `MERPAGO*`, `CFE`, `PAYPAL *SPOTIFY`) categorize accurately via an LLM-forward pipeline (PII redacted before any external call), learning from corrections. | Owner, Member |
| Shared household finances | A genuinely collaborative ledger — joint budgets, per-member attribution, shared goals — which no Mexico-focused product offers. | Owner, Member |

### Explicitly out of scope (v1)

- **Live bank aggregation (Belvo/Finerio)** — paid B2B integration + consent/regulatory weight; a post-v1 optional connector, not v1.
- **Bill negotiation / concierge** — US-specific human-ops business; no Mexican equivalent.
- **Credit-score monitoring** — requires regulated Buró de Crédito integration.
- **Deep investment / portfolio analytics** — v1 tracks account balances for net worth only; no holdings-level analytics, crypto, or property valuation.
- **Receipt OCR** — statement parsing is the priority; receipts later.
- **Financial-product marketplace (loans/insurance)** — monetization play, not core PFM.
- **Moving money / bill pay** — the-ledger only reads and organizes; it never initiates payments or transfers. This boundary is permanent (also keeps the system out of money-transmitter regulation).
- **Envelope / zero-based budgeting** — v1 is flexible category targets; envelope mode is a later option the model leaves room for.

## RBAC model (initial)

Roles bind to capabilities, not to UI screens. Tenant = one household.

- **owner** — full control of their own tenant: manage accounts, upload/delete statements, edit any transaction, set budgets/goals, invite/remove members, configure alerts, export or delete the household's data, manage billing. Cannot see any other tenant.
- **member** — within their household: view everything, add/edit/recategorize transactions, view budgets/goals/insights. Cannot manage members, change billing, or delete the household.
- **viewer** — read-only within their household: view dashboards, feed, and reports. Cannot edit anything. (E.g. an accountant or a family member who should only look.)
- **operator** — platform/instance administration: provision tenants, monitor health, manage bank-parser and categorization configuration, and execute data-subject (ARCO/GDPR) export/delete requests. Cannot read tenant financial data except through explicitly audited support tooling.

## Regulatory constraints

- **LFPDPPP (Mexico data protection)** — must present an *aviso de privacidad* and capture explicit consent at signup; must honor **ARCO rights** (Acceso, Rectificación, Cancelación, Oposición) → implies per-user **data export and permanent delete** from day one.
- **GDPR (if any EU residents use it)** — right to erasure, data portability/export, and a lawful basis for processing. Satisfied by the same export/delete + consent machinery built for LFPDPPP.
- **PCI-DSS** — statement PDFs can contain card numbers; the system must **mask/redact full PANs at ingestion and never persist a full card number**, keeping PCI scope minimal.
- **Financial data protection** — account numbers, CLABE, balances, and names must be **encrypted at rest and in transit**, with strict **per-tenant isolation** and **an audit trail on every access** to financial data. Never commit real statements or tenant data to the public repo (synthetic fixtures only).
- **LLM data handling** — even in the LLM-forward categorization mode, transaction text must be **PII-redacted (names, account numbers, CLABE) before any external model call**, with a per-user opt-out to a rules-only mode.
- **Not a money transmitter** — the read-only / never-move-money boundary must be preserved to stay outside Ley Fintech Title II (ITF) licensing.

## Success metrics

Each is observable in system telemetry.

- **Time to first parsed statement** — median < **5 minutes** from signup to first statement uploaded and transactions confirmed. (Telemetry: `signup_ts` → `ingestion_confirmed_ts`.)
- **Statement parse accuracy** — ≥ **95%** of transactions extracted correctly (no manual correction) on supported banks. (Telemetry: extracted-row count vs user-corrected-row count per statement.)
- **Auto-categorization acceptance** — ≥ **85%** of suggested categories accepted without change. (Telemetry: suggested vs overridden category events.)
- **Mobile primary-flow completion** — ≥ **70%** of "review & categorize" sessions on viewports < 480px complete without error or desktop fallback. (Telemetry: session viewport width + task-completion event.)
- **Week-1 activation** — ≥ **50%** of new tenants upload ≥ 1 statement **and** set ≥ 1 budget within 7 days. (Telemetry: per-tenant `statement_uploaded` and `budget_set` events.)

## Open questions for plan-system

1. **Scale target for v1** — tens of tenants (one shared database with row-level tenant isolation) or thousands (heavier isolation / sharding)? Drives the multi-tenancy model.
   - *Answer (default):* **One shared database with row-level tenant isolation** (`tenant_id` on every row, enforced centrally), sized for hundreds-to-low-thousands of tenants. Fits the serverless/scale-to-zero target without per-tenant DB overhead. Revisit as an ADR.
2. **Statement-parsing strategy** — deterministic per-bank templates as the primary path with AI extraction as fallback, or an AI-extraction-first pipeline validated against templates? Drives the parser module design and the parse-accuracy metric.
   - *Answer (default):* **AI-extraction-first** (aligns with the LLM-forward decision), with **per-bank format hints and a balance-reconciliation validation pass** to catch errors, plus a **deterministic fast-path for clean digital-bank CSV** to cap cost. Revisit as an ADR.
3. **Deployment model** — is the multi-tenant SaaS instance the *only* shape, or should a single-tenant self-host deployment also be a first-class target for operators? Affects tenancy and onboarding.
   - *Answer (default):* **Single multi-tenant codebase is primary.** Self-hosting is the same image run with a single tenant — no separate single-tenant build. Keeps one path to maintain.
4. **Authentication boundary** — self-managed email/password vs an external identity provider (social / hosted IdP)? Affects the auth module and signup flow.
   - *Answer (default):* **Managed external identity provider** (email/password + social sign-in), so the system never stores raw passwords. Concrete provider chosen in architecture.
5. **Alert/notification delivery** — in-app only for v1, or email/push as well? Affects whether a notification connector is in v1 scope.
   - *Answer (default):* **In-app + email for v1** (email matters for "new fee"/"low balance" alerts when the app is closed); push notifications deferred.
