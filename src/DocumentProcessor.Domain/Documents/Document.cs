namespace DocumentProcessor.Domain.Documents;

public sealed class Document
{
    private Document(
        Guid id,
        string fileName,
        string mediaType,
        long sizeBytes,
        string sha256,
        DocumentStatus status,
        DocumentAnalysis? analysis,
        DocumentProcessingMetadata? processingMetadata,
        string? failureReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        FileName = fileName;
        MediaType = mediaType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        Status = status;
        Analysis = analysis;
        ProcessingMetadata = processingMetadata;
        FailureReason = failureReason;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; }
    public string FileName { get; }
    public string MediaType { get; }
    public long SizeBytes { get; }
    public string Sha256 { get; }
    public DocumentStatus Status { get; private set; }
    public DocumentAnalysis? Analysis { get; private set; }
    public DocumentProcessingMetadata? ProcessingMetadata { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Document Create(
        string fileName,
        string mediaType,
        long sizeBytes,
        string sha256,
        DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            fileName,
            mediaType,
            sizeBytes,
            sha256,
            DocumentStatus.Pending,
            null,
            null,
            null,
            now,
            now);

    public static Document Restore(
        Guid id,
        string fileName,
        string mediaType,
        long sizeBytes,
        string sha256,
        DocumentStatus status,
        DocumentAnalysis? analysis,
        DocumentProcessingMetadata? processingMetadata,
        string? failureReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc) =>
        new(
            id,
            fileName,
            mediaType,
            sizeBytes,
            sha256,
            status,
            analysis,
            processingMetadata,
            failureReason,
            createdAtUtc,
            updatedAtUtc);

    public void StartProcessing(DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.Pending);
        Status = DocumentStatus.Processing;
        UpdatedAtUtc = now;
    }

    public void Complete(
        DocumentAnalysis analysis,
        DocumentProcessingMetadata processingMetadata,
        DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.Processing);
        Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
        ProcessingMetadata = processingMetadata ?? throw new ArgumentNullException(nameof(processingMetadata));
        FailureReason = null;
        Status = DocumentStatus.Completed;
        UpdatedAtUtc = now;
    }

    public void Fail(
        string reason,
        DateTimeOffset now,
        DocumentProcessingMetadata? processingMetadata = null)
    {
        EnsureStatus(DocumentStatus.Processing);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.", nameof(reason));

        FailureReason = reason.Trim();
        ProcessingMetadata = processingMetadata;
        Status = DocumentStatus.Failed;
        UpdatedAtUtc = now;
    }

    private void EnsureStatus(DocumentStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Document {Id} must be in status {expected} but is {Status}.");
    }
}
