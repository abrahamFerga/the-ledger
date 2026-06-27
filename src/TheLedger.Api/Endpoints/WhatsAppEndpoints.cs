using TheLedger.Api.Setup;
using TheLedger.Application.Authorization;
using TheLedger.Application.Channels;

namespace TheLedger.Api.Endpoints;

public static class WhatsAppEndpoints
{
    /// <summary>
    /// WhatsApp connector surface (feature #50, ADR-0010): the Meta inbound webhook plus the per-user
    /// opt-in management endpoints. The webhook lives under the connector route group
    /// <c>/api/v1/connectors/whatsapp/webhook</c> (never a top-level route, per the connector contract).
    /// </summary>
    public static void MapWhatsApp(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        var webhook = v1.MapGroup("/connectors/whatsapp/webhook").WithTags("WhatsApp");

        // GET: Meta's subscription verify-token challenge. JWT-anonymous (Meta calls it), gated by the
        // verify token. Echoes hub.challenge as text/plain when the token matches, else 403.
        webhook.MapGet("/", (
                HttpContext http, IWhatsAppWebhookHandler handler) =>
            {
                var mode = http.Request.Query["hub.mode"].FirstOrDefault();
                var token = http.Request.Query["hub.verify_token"].FirstOrDefault();
                var challenge = http.Request.Query["hub.challenge"].FirstOrDefault();

                var echoed = handler.Verify(mode, token, challenge);
                return echoed is null
                    ? Results.StatusCode(StatusCodes.Status403Forbidden)
                    : Results.Text(echoed, "text/plain");
            })
            .AllowAnonymous()
            .WithName("WhatsAppWebhookVerify");

        // POST: inbound messages. JWT-anonymous (Meta calls it); the HMAC-SHA256 of the RAW body against
        // the app secret is verified inside the handler BEFORE any processing — an unverified call is
        // rejected 403. Exempt from the Idempotency-Key middleware (see IdempotencyMiddleware): Meta does
        // not send that header, so dedupe is on the WhatsApp message id instead.
        //
        // Outcomes: 413 if the body exceeds the cap, 403 on bad signature, 200 on success, 503 on a
        // transient processing failure. We deliberately do NOT swallow-then-200 a processing failure:
        // paired with the inbound processor's compensating dedupe-release (FIX 2), a 503 lets Meta's
        // bounded backoff retry re-process and stage the capture instead of silently dropping it.
        // FUTURE HARDENING: move staging fully off the request path (inbox-via-outbox) so this handler
        // only verifies + enqueues and always returns 200 fast; that refactor is out of scope here.
        webhook.MapPost("/", async (HttpContext http, IWhatsAppWebhookHandler handler,
                ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TheLedger.Api.Endpoints.WhatsAppWebhook");

                byte[] rawBody;
                try
                {
                    rawBody = await ReadRawBodyAsync(http, ct);
                }
                catch (InvalidOperationException)
                {
                    // Body exceeded the cap — reject before buffering more (anti-memory-exhaustion).
                    return Results.Problem(
                        title: "Payload too large",
                        detail: $"The webhook body exceeds the {MaxBodyBytes / 1024} KB limit.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                var signature = http.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

                WhatsAppWebhookResult result;
                try
                {
                    result = await handler.HandleAsync(rawBody, signature, ct);
                }
                catch (Exception ex)
                {
                    // Boundary catch: log internally (no stack trace leaks to Meta) and surface as a
                    // transient failure. With FIX 2's dedupe-release the retry re-processes productively.
                    logger.LogError(ex, "WhatsApp webhook processing failed; signaling transient failure for retry");
                    return Results.Problem(
                        title: "Temporarily unavailable",
                        detail: "The message could not be processed right now; please retry.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                if (!result.SignatureValid)
                {
                    return Results.Problem(
                        title: "Invalid signature",
                        detail: "The X-Hub-Signature-256 header did not match the request body.",
                        statusCode: StatusCodes.Status403Forbidden);
                }

                // Meta expects a 200 quickly; processing is synchronous here but cheap (text → parser,
                // image → an outbox enqueue for the worker's OCR).
                return Results.Ok(new { processed = result.Processed });
            })
            .AllowAnonymous()
            .WithName("WhatsAppWebhookReceive");

        // Per-user opt-in management (RBAC-gated, tenant-scoped). Opt-in records a WhatsAppChannel consent
        // and maps the phone number to the user so inbound captures resolve to them.
        var optIn = v1.MapGroup("/connectors/whatsapp/opt-in").WithTags("WhatsApp");
        optIn.MapGet("/", async (IWhatsAppOptInService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(ct)))
            .RequireAuthorization(Policies.MembersManage);
        optIn.MapPost("/", async (WhatsAppOptInRequest req, IWhatsAppOptInService svc, CancellationToken ct) =>
                Results.Ok(await svc.OptInAsync(req, ct)))
            .RequireAuthorization(Policies.MembersManage);
        optIn.MapDelete("/{contactId:guid}", async (Guid contactId, IWhatsAppOptInService svc, CancellationToken ct) =>
            {
                await svc.RevokeAsync(contactId, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.MembersManage);
    }

    /// <summary>
    /// Cap on the buffered webhook body. Meta's webhook payloads are small JSON envelopes (media is fetched
    /// separately by media id, never inlined), so 256 KB is generous; the cap stops a hostile caller from
    /// using the buffered read to exhaust memory.
    /// </summary>
    private const int MaxBodyBytes = 256 * 1024;

    /// <summary>
    /// Buffers the raw request body (needed verbatim for the HMAC), enforcing <see cref="MaxBodyBytes"/>.
    /// Honors Content-Length when present and also caps the streamed copy, so a missing/spoofed length can't
    /// bypass the limit. Throws <see cref="InvalidOperationException"/> when the body is oversize.
    /// </summary>
    private static async Task<byte[]> ReadRawBodyAsync(HttpContext http, CancellationToken ct)
    {
        if (http.Request.ContentLength is > MaxBodyBytes)
        {
            throw new InvalidOperationException("WhatsApp webhook body exceeds the maximum allowed size.");
        }

        http.Request.EnableBuffering();
        using var ms = new MemoryStream();
        // Cap the copy regardless of Content-Length: read one byte past the limit and reject if reached.
        var buffer = new byte[8 * 1024];
        int read;
        while ((read = await http.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (ms.Length > MaxBodyBytes)
            {
                throw new InvalidOperationException("WhatsApp webhook body exceeds the maximum allowed size.");
            }
        }

        http.Request.Body.Position = 0;
        return ms.ToArray();
    }
}
