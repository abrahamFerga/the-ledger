# WhatsApp connector

A WhatsApp Business number that turns an inbound **receipt photo** or **natural-language message**
("gasté 200 en el Oxxo") into a **staged** transaction in the existing review-and-confirm queue, and
sends outbound **bill-due / anomaly / export-ready** alerts to opted-in users. Feature #50, ADR-0010.

This is a **channel** connector in the pluggable-connector contract. It mirrors the email connector's
shape: the rest of the system depends only on the `IChannel` / `IWhatsAppSender` abstractions in
`TheLedger.Application.Channels` and on domain types — never on a provider SDK. The Meta WhatsApp
Business Cloud API specifics (HMAC verification, webhook envelope parsing, outbound send, media
download) are partitioned inside `TheLedger.Infrastructure.Connectors.WhatsApp`, so the provider is
swappable (ACS Advanced Messaging / Twilio) without touching handlers.

## What it does

- **Inbound webhook** (`/api/v1/connectors/whatsapp/webhook`):
  - `GET` answers Meta's subscription **verify-token** challenge (`hub.mode` / `hub.verify_token` /
    `hub.challenge`).
  - `POST` validates the **HMAC-SHA256** signature of the **raw** body against the app secret
    (`X-Hub-Signature-256: sha256=…`) **before** any processing; an unverified call is rejected `403`.
  - **Dedupes** on the WhatsApp message id (Redis, the same store the idempotency middleware uses).
  - Resolves the sender phone → an **opted-in** user. Unknown / not-opted-in senders get a generic help
    reply and **no tenant data** (never leaks across households).
  - Inbound **image** → reuses `IReceiptExtractor` (#49); inbound **text** → reuses
    `INaturalLanguageTransactionParser` (#51). Both produce a **staged** transaction.
- **Outbound** send via `IWhatsAppSender`, routed through the **outbox** (never inline). A deterministic
  fake sender backs dev/CI/tests.

## Configuration (`WhatsApp` section / Key Vault)

| Key | Secret | Purpose |
|---|---|---|
| `WhatsApp:VerifyToken` | `whatsapp-verify-token` | Echoed on the GET subscription challenge. |
| `WhatsApp:AppSecret` | `whatsapp-app-secret` | HMAC key for the `X-Hub-Signature-256` body signature. |
| `WhatsApp:AccessToken` | `whatsapp-access-token` | Meta Graph token for outbound send + media download. |
| `WhatsApp:PhoneNumberId` | — | The WhatsApp number id outbound messages send from. |

With **no `AccessToken`/`PhoneNumberId`** the connector runs in **dev/fake mode**: the verify-token and
HMAC checks still apply (so the webhook is exercisable), but outbound sends and media downloads use the
deterministic fakes — CI needs no real Meta credentials. Secrets come from the cloud secret store, never
`appsettings.json` or this folder.

## Per-user opt-in

Opt-in is a `ConsentRecord` of type `WhatsAppChannel`. A `WhatsAppContact` row maps a phone number
(E.164 digits) → user within a tenant. Inbound resolution requires both: a matching contact **and** a
current `WhatsAppChannel` consent for that user.
