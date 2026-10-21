using DocumentProcessor.Domain.Documents;

namespace DocumentProcessor.Application.Documents.Models;

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string MediaType,
    long SizeBytes,
    string Sha256,
    DocumentStatus Status,
    DocumentAnalysis? Analysis,
    DocumentProcessingMetadata? ProcessingMetadata,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static DocumentResponse FromDomain(Document document) =>
        new(
            document.Id,
            document.FileName,
            document.MediaType,
            document.SizeBytes,
            document.Sha256,
            document.Status,
            document.Analysis,
            document.ProcessingMetadata,
            document.FailureReason,
            document.CreatedAtUtc,
            document.UpdatedAtUtc);
}
