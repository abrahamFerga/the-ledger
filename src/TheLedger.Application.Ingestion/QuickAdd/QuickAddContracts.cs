using TheLedger.Domain.Common;
using TheLedger.Domain.Ledger;

namespace TheLedger.Application.Ingestion.QuickAdd;

/// <summary>
/// A free-text or dictated phrase to parse into a transaction draft, e.g.
/// "comí 350 en restaurante ayer" or "gasté 200 en el Oxxo". Optional account hint pre-selects the
/// account the confirmed transaction will land on (the draft itself never persists).
/// </summary>
public sealed record QuickAddRequest(string Text, Guid? AccountId = null);

/// <summary>
/// The parsed, schema-validated transaction draft returned by <see cref="INaturalLanguageTransactionParser"/>.
/// This is a <b>transient</b> draft — it is surfaced to the user for explicit confirmation and is never
/// persisted by the quick-add path (ADR-0011, confirm-before-persist). On confirm, the SPA replays it
/// through the existing manual-transaction create endpoint.
/// </summary>
/// <remarks>
/// <paramref name="Date"/> is resolved relative to <i>today</i> in <c>America/Mexico_City</c> so relative
/// phrases ("ayer", "antier", "el lunes") land on the correct calendar day regardless of server timezone.
/// </remarks>
public sealed record TransactionDraft(
    decimal Amount,
    string Currency,
    DateOnly Date,
    TransactionDirection Direction,
    [property: Pii] string? Merchant,
    Guid? ProposedCategoryId,
    double Confidence);

/// <summary>
/// Parses a natural-language phrase into a <see cref="TransactionDraft"/>. The production implementation
/// is LLM-forward (the <c>QuickAddParserAgent</c>, the existing Azure OpenAI <c>IChatClient</c>, ADR-0011)
/// with structured output; a deterministic fake backs dev/tests and the no-model fallback. The same parser
/// is reused by inbound WhatsApp text (#50). The returned draft is never persisted without explicit user
/// confirmation — the caller surfaces it for confirm/edit.
/// </summary>
public interface INaturalLanguageTransactionParser
{
    Task<TransactionDraft> ParseAsync(QuickAddRequest request, CancellationToken ct);
}
