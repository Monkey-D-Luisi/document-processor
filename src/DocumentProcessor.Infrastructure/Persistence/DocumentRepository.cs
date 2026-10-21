using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Domain.Documents;
using DocumentProcessor.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.Infrastructure.Persistence;

[IoC(typeof(IDocumentRepository), IoCLifetime.Scoped)]
internal sealed class DocumentRepository(AppDbContext dbContext)
    : IDocumentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Documents.AddAsync(ToRecord(document), cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Documents
            .SingleAsync(item => item.Id == document.Id, cancellationToken);

        record.Status = document.Status.ToString();
        record.AnalysisJson = document.Analysis is null
            ? null
            : JsonSerializer.Serialize(document.Analysis, JsonOptions);
        record.ProcessingMetadataJson = document.ProcessingMetadata is null
            ? null
            : JsonSerializer.Serialize(document.ProcessingMetadata, JsonOptions);
        record.FailureReason = document.FailureReason;
        record.UpdatedAtUtc = document.UpdatedAtUtc;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DocumentRecord ToRecord(Document document) =>
        new()
        {
            Id = document.Id,
            FileName = document.FileName,
            MediaType = document.MediaType,
            SizeBytes = document.SizeBytes,
            Sha256 = document.Sha256,
            Status = document.Status.ToString(),
            AnalysisJson = document.Analysis is null
                ? null
                : JsonSerializer.Serialize(document.Analysis, JsonOptions),
            ProcessingMetadataJson = document.ProcessingMetadata is null
                ? null
                : JsonSerializer.Serialize(document.ProcessingMetadata, JsonOptions),
            FailureReason = document.FailureReason,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
        };

    private static Document ToDomain(DocumentRecord record)
    {
        if (!Enum.TryParse<DocumentStatus>(record.Status, ignoreCase: true, out var status))
            throw new InvalidOperationException(
                $"Unknown persisted document status '{record.Status}'.");

        var analysis = record.AnalysisJson is null
            ? null
            : JsonSerializer.Deserialize<DocumentAnalysis>(record.AnalysisJson, JsonOptions);

        var processingMetadata = record.ProcessingMetadataJson is null
            ? null
            : JsonSerializer.Deserialize<DocumentProcessingMetadata>(
                record.ProcessingMetadataJson,
                JsonOptions);

        return Document.Restore(
            record.Id,
            record.FileName,
            record.MediaType,
            record.SizeBytes,
            record.Sha256,
            status,
            analysis,
            processingMetadata,
            record.FailureReason,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
