# Industry research: personal-finance

> Scope: consumer **personal finance management (PFM)** apps — budgeting,
> transaction tracking, net worth. Geography lens: **Mexico-first** (the owner is
> in Mexico) while drawing on the global English-speaking leaders for capability
> parity. Segment: **B2C / household** (an individual or couple managing their own
> money), not SMB accounting.

## Top commercial players

1. **Monarch Money** — comprehensive Mint successor; the benchmark for *household / couples* money management. Founded 2018 (by ex-Mint people). Surged after Mint's March 2024 shutdown. ~hundreds of thousands of paying households. Segment: consumer (premium, ~$100/yr). iOS + Android + web.
2. **YNAB (You Need A Budget)** — the *methodology* leader: zero-based / envelope budgeting ("give every dollar a job"). Founded 2004. ~1M+ users. Segment: consumer (premium, ~$109/yr). iOS + Android + web. Strong education/community.
3. **Copilot Money** — the *AI-categorization + design* leader. Founded 2019. Apple-ecosystem-first (iOS/macOS native; a limited web app shipped Dec 2025; still **no Android**). Best-in-class auto-categorization and net-worth/investment view. Segment: consumer (~$95/yr).
4. **Rocket Money** (formerly Truebill) — the *subscription tracking, bill negotiation & automated savings* leader; capable free tier monetized by a concierge upsell. Owned by Rocket Companies. Millions of users. Segment: consumer (freemium). iOS + Android + web.
5. **Fintonic** — the *Mexico / LatAm + Spain* leader. Automatic bank aggregation, automatic categorization, and alerting (duplicate charges, fees, low balance); monetizes via a loan/insurance marketplace + FinScore. Strongest **Mexican bank coverage** of the five. Segment: consumer (free). iOS + Android.

> Two **infrastructure** players matter for context but are not consumer apps, so
> they're not scored below: **Belvo** (LatAm open-finance aggregation, ~80M
> connected accounts, bilateral bank deals) and **Finerio Connect** (Mexico City
> white-label aggregation/categorization). They are how a fintech *would* get live
> Mexican bank data today — as a paid B2B integration, not a free API. Relevant to
> the "can I connect to my bank?" question (see Open questions + Compliance).

## Capability matrix

Three signal levels: `✓ deep` (marketed differentiator / heavy investment), `✓ basic` (present but minimal), `—` (absent). Footnotes mark cells I inferred rather than verified on a live page.

| Capability | Monarch | YNAB | Copilot | Rocket | Fintonic |
|---|---|---|---|---|---|
| Automatic bank sync (aggregation) | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ deep |
| **Mexican bank coverage** | — ¹ | — ¹ | — ¹ | — ¹ | ✓ deep |
| **PDF statement import & parsing** | — | — | — | — | — |
| CSV / OFX / QFX import | ✓ basic | ✓ basic | ✓ basic | — | — |
| Manual transaction entry | ✓ deep | ✓ deep | ✓ deep | ✓ basic | ✓ basic |
| Receipt capture / OCR | — | — | — | — | — |
| Multi-currency | ✓ basic | ✓ basic ² | ✓ basic | — | ✓ basic |
| Auto-categorization (rules) | ✓ deep | ✓ basic | ✓ deep | ✓ deep | ✓ deep |
| AI / ML categorization | ✓ basic | — | ✓ deep | ✓ basic | ✓ basic |
| User rules engine | ✓ deep | ✓ basic | ✓ deep | ✓ basic | ✓ basic |
| Envelope / zero-based budgeting | ✓ basic | ✓ deep | ✓ basic | — | — |
| Flexible category-target budgeting | ✓ deep | ✓ basic | ✓ deep | ✓ basic | ✓ basic |
| Recurring-transaction detection | ✓ deep | ✓ basic | ✓ deep | ✓ deep | ✓ basic |
| Subscription tracking & cancel | ✓ basic | — | ✓ deep | ✓ deep | ✓ basic |
| Bill reminders / due dates | ✓ deep | ✓ deep | ✓ basic | ✓ deep | ✓ deep |
| Cash-flow forecasting | ✓ deep | ✓ basic | ✓ basic | ✓ basic | ✓ basic |
| Net-worth tracking | ✓ deep | ✓ basic | ✓ deep | ✓ basic | ✓ basic |
| Investment / portfolio tracking | ✓ deep | ✓ basic | ✓ deep | ✓ basic | ✓ basic |
| Account aggregation (one view) | ✓ deep | ✓ deep | ✓ deep | ✓ deep | ✓ deep |
| Goals & savings tracking | ✓ deep | ✓ deep | ✓ basic | ✓ deep | ✓ basic |
| Debt payoff planning | ✓ basic | ✓ deep | ✓ basic | ✓ basic | ✓ basic |
| Spending insights / trends / reports | ✓ deep | ✓ basic | ✓ deep | ✓ basic | ✓ deep |
| Custom reports & export | ✓ deep | ✓ basic | ✓ basic | — | ✓ basic |
| Alerts & anomaly detection | ✓ basic | — | ✓ basic | ✓ deep | ✓ deep |
| Credit-score monitoring | — | — | — | ✓ deep | ✓ basic ³ |
| Household / shared (multi-user) | ✓ deep | ✓ basic | ✓ basic | — | — |
| iOS + Android apps | ✓ deep | ✓ deep | ✓ basic ⁴ | ✓ deep | ✓ deep |
| Web app | ✓ deep | ✓ deep | ✓ basic ⁴ | ✓ basic | ✓ basic |
| Bill negotiation / concierge | — | — | — | ✓ deep | — |
| Financial-product marketplace | — | — | — | ✓ basic | ✓ deep |

Footnotes:
1. US/Canada-centric aggregation (Plaid/MX/Finicity). Mexican retail-bank coverage is weak-to-absent; not a marketed market for these four.
2. YNAB supports a single currency per budget, not multi-currency within one budget.
3. Fintonic surfaces a creditworthiness score ("FinScore") tied to its loan marketplace rather than a bureau credit score.
4. Copilot is Apple-first; a **limited** web app launched Dec 2025 and there is still no Android client.

## Synthesized capabilities

### Must-have (v1)

Capabilities present (≥ `✓ basic`) in at least 4 of 5 players — table-stakes for a credible PFM. **One deliberate substitution:** the universal "automatic bank sync" is *not reliably achievable in Mexico* (see Compliance), so for `the-ledger` its table-stakes role is filled by **PDF/CSV/manual ingestion**. That makes PDF parsing a must-have *for us* even though no competitor offers it.

- **Transaction ingestion (PDF + CSV + manual)** — upload a Mexican bank-statement PDF and extract transactions (fecha, descripción, cargo/abono, saldo); plus CSV import and manual entry. *This replaces aggregation as the v1 on-ramp.*
- **Account aggregation (one view)** — all accounts (checking, savings, cards, cash) rolled into one balance/feed.
- **Auto-categorization** — rule-based categorization of transactions out of the box, learning from user corrections.
- **Manual entry & editing** — add/split/edit/recategorize any transaction.
- **Flexible category-target budgeting** — monthly budget per category, spent-vs-target tracking.
- **Recurring-transaction detection** — identify salary, rent, subscriptions automatically.
- **Bill reminders / due dates** — upcoming-bill calendar and reminders.
- **Net-worth tracking** — assets − liabilities over time.
- **Goals & savings tracking** — named savings goals with progress.
- **Spending insights / reports** — by category, by month, trends; exportable.
- **Alerts & anomaly detection** — duplicate charges, unusual spend, low balance, new fees (Fintonic's signature, and high-value in MX).
- **Mobile-first responsive UI** — primary flows usable one-handed on a phone (owner requirement); PWA-installable.

### Differentiator (v1)

High-impact capabilities thin across the field — our "why us." Capped at 3:

1. **Mexican bank-statement PDF parsing** — the wedge. *No competitor parses statements*; in a market with no real open banking, "upload your PDF and it just works" is the headline feature. Exemplar: none — this is greenfield.
2. **AI-assisted categorization tuned for Mexican Spanish merchants** — Copilot proves AI categorization is the UX bar; we extend it to messy Spanish-language MX merchant strings (e.g. `OXXO`, `MERPAGO*`, `CFE`, `PAYPAL *SPOTIFY`) using an LLM, with a privacy-preserving redaction step. Exemplar: **Copilot**.
3. **Household / multi-tenant sharing** — a shared household ledger with per-member logins and roles, aligning with the multi-tenant-SaaS decision. Exemplar: **Monarch**.

### Skip for v1

Naming what's *out* is load-bearing. Deferred:

- **Live bank aggregation (Belvo/Finerio)** — defer to an *optional, paid, post-v1 connector*. Real cost + B2B contracting + consent/regulatory weight; PDF/CSV covers v1.
- **Bill negotiation / concierge (Rocket)** — US-specific, human-ops business; no MX equivalent.
- **Credit-score monitoring** — needs Buró de Crédito integration (regulated, contracted); out of scope.
- **Deep investment / portfolio analytics** — basic net-worth account balances only in v1; no holdings-level analytics, crypto wallets, or Zillow-style asset valuation.
- **Receipt OCR** — nice-to-have; statement parsing is the priority OCR investment.
- **Financial-product marketplace (loans/insurance)** — monetization play, not core PFM; out of scope.
- **Bank bill-pay / money movement** — never. `the-ledger` reads and organizes; it does not move money (also keeps us out of money-transmitter regulation).

## Notable UX patterns observed

These recur across players and constrain the dashboard design downstream.

- **Unified transaction feed as the home surface** — the "inbox" of money; everything else hangs off it. Seen in: all five.
- **Review-and-categorize flow** — a queue of uncategorized/low-confidence transactions to triage, swipe-to-categorize on mobile. Seen in: Copilot, Monarch, Fintonic.
- **Net-worth-over-time hero chart** — a single trend line as the emotional anchor of the home screen. Seen in: Monarch, Copilot.
- **Budget progress bars** — category rows with spent/target and a fill bar; color shifts as you approach/exceed. Seen in: Monarch, YNAB, Copilot.
- **Recurring / upcoming-bills calendar** — a forward-looking view distinct from the historical feed. Seen in: Monarch, Rocket, Fintonic.
- **Alert stream / notifications center** — anomalies and reminders as a reviewable list. Seen in: Rocket, Fintonic.
- **Connect-account onboarding** — the first-run "link your bank" step. *We invert this to "upload your first statement / add an account manually,"* the single biggest onboarding divergence from the field.
- **Couples/household shared view with separate logins** — joint accounts + individual identity. Seen in: Monarch (benchmark).

## Compliance / regulatory considerations

- **Open banking is not available in Mexico (practically).** Ley Fintech (2018) mandated open finance, but CNBV's secondary regulations remain largely unissued through 2025–2026 — implemented rules cover only ATM "open data." Consequence: a self-serve consumer app **cannot reliably pull live bank data via standardized APIs**; aggregation requires a paid B2B provider (Belvo/Finerio) operating under bilateral bank agreements. This is the architectural justification for PDF-first ingestion.
- **LFPDPPP (Ley Federal de Protección de Datos Personales en Posesión de los Particulares)** — Mexico's data-protection law. Requires a privacy notice (*aviso de privacidad*), explicit consent, and honoring **ARCO rights** (Acceso, Rectificación, Cancelación, Oposición). Constrains: consent capture at signup, per-user data export + deletion.
- **GDPR** — if "usable by others" includes EU residents, applies: data minimization, right to erasure, data portability/export, lawful basis. Cheapest to honor by building export + delete from day one (also satisfies ARCO).
- **PCI-DSS** — only triggered if we store full card numbers (PANs). Bank-statement PDFs **can contain card numbers** → **mask/redact card numbers at ingestion**; never persist a full PAN. Staying out of PAN storage keeps PCI scope minimal.
- **Financial data at rest** — statements carry account numbers, CLABE, balances, names → **encrypt at rest and in transit, strict per-tenant isolation**, audit every access. Public-repo discipline: never commit real statements or tenant data (use synthetic fixtures).
- **Not a money transmitter** — `the-ledger` only reads/organizes; it never initiates payments or moves money, which keeps it out of ITF/money-transmitter licensing (Ley Fintech Title II). Preserve this boundary deliberately.
- **LLM data handling** — sending transaction descriptions to a hosted LLM for categorization exports (possibly personal) financial strings to a third party. Needs a redaction step (strip names/account numbers) + a consent toggle, or an on-prem/rules-only mode. See Open question 4.

## Open questions for the user

Numbered so you can answer by index. Recommended defaults are noted; answers recorded inline once given.

1. **Which Mexican banks' statement formats should v1 parse first?** Each bank's PDF layout differs, so coverage is per-bank work. Recommended default: start with **BBVA, Santander, Banorte** (the three largest retail banks), then Citibanamex/HSBC.
   - *Answer:* **BBVA, Santander, Banorte, and Nu/digital banks (Nubank, Hey Banco, Klar)** for v1. Digital-bank statements/CSV are cleaner and a good early parser target; the big-three PDFs are the harder, higher-value work.
2. **Budgeting philosophy for v1: YNAB-style envelope/zero-based, or Monarch-style flexible category targets (or both)?** This shapes the core data model. Recommended default: **flexible category targets** in v1 (lower learning curve, matches Monarch/Copilot), with envelope mode as a later option.
   - *Answer:* **Flexible category targets** for v1 (Monarch/Copilot style). Envelope/zero-based deferred.
3. **AI categorization: OK to send redacted transaction descriptions to a hosted LLM (Azure OpenAI), or keep categorization rules-only / on-prem for privacy?** Recommended default: **hybrid** — fast local rules first; LLM only for low-confidence remainder, with PII redaction + a per-user opt-out.
   - *Answer:* **LLM-forward** — lean on the LLM (Azure OpenAI) for categorization to maximize accuracy on messy Mexican-Spanish merchant strings. Baseline PII redaction (strip names/account numbers/CLABE) still applies before any external call, but categorization prioritizes accuracy over minimizing model calls. A rules layer still runs first for cheap high-confidence hits and to cap cost.
4. **Primary currency: MXN-only in v1, or multi-currency from the start?** Recommended default: **MXN-first, multi-currency-ready** schema (store currency per account/transaction; single display currency in v1).
   - *Answer:* **MXN-first, multi-currency-ready.** Currency stored per account/transaction; MXN is the display currency in v1; FX conversion deferred.
5. **Live bank aggregation (Belvo) — confirm it's a post-v1 optional connector, not v1 scope?** Recommended default: **yes, defer**; v1 is PDF + CSV + manual only.
   - *Answer:* **Deferred** (accepted default). Live aggregation (Belvo/Finerio) is a post-v1 optional connector; v1 ingestion is PDF + CSV + manual only.
