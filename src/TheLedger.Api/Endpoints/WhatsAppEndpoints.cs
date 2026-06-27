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
        webhook.MapPost("/", async (HttpContext http, IWhatsAppWebhookHandler handler, CancellationToken ct) =>
            {
                var rawBody = await ReadRawBodyAsync(http, ct);
                var signature = http.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

                var result = await handler.HandleAsync(rawBody, signature, ct);
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

    private static async Task<byte[]> ReadRawBodyAsync(HttpContext http, CancellationToken ct)
    {
        http.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await http.Request.Body.CopyToAsync(ms, ct);
        http.Request.Body.Position = 0;
        return ms.ToArray();
    }
}
