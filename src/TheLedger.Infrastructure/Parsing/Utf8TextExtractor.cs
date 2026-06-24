using System.Text;
using TheLedger.Application.Ingestion.Extraction;

namespace TheLedger.Infrastructure.Parsing;

/// <summary>
/// Bootstrap text extractor: decodes statement bytes as UTF-8. Real PDF byte→text extraction is the
/// AI / Azure Document Intelligence path (ADR-0002/0004); a vetted local PDF library can be slotted
/// in behind <see cref="IPdfTextExtractor"/> once verified (the available PdfPig build was an
/// untrusted "-custom" prerelease and was deliberately not added to a public repo).
/// </summary>
public sealed class Utf8TextExtractor : IPdfTextExtractor
{
    public string ExtractText(byte[] content) =>
        content.Length == 0 ? string.Empty : Encoding.UTF8.GetString(content);
}
