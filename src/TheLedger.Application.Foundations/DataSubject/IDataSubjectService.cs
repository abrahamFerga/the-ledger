namespace TheLedger.Application.Foundations.DataSubject;

public sealed record DataExportDto(Guid TenantId, DateTimeOffset GeneratedAt, string Format, object Data);

/// <summary>
/// LFPDPPP/GDPR data-subject operations: portable export and permanent per-tenant deletion.
/// Export gathers every <c>[Pii]</c>-bearing record for the tenant; delete is irreversible.
/// </summary>
public interface IDataSubjectService
{
    Task<DataExportDto> ExportAsync(Guid tenantId, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, CancellationToken ct);
}
