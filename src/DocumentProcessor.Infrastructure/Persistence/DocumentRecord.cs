namespace DocumentProcessor.Infrastructure.Persistence;

internal sealed class DocumentRecord
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AnalysisJson { get; set; }
    public string? ProcessingMetadataJson { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
