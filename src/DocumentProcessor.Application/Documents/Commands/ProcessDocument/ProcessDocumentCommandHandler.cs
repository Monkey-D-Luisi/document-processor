using System.Security.Cryptography;
using DocumentProcessor.Application.Documents.Analysis.Exceptions;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Application.Documents.Models;
using DocumentProcessor.Application.Documents.Analysis.Services;
using DocumentProcessor.Application.Documents.Analysis.Models;
using DocumentProcessor.Domain.Documents;
using DocumentProcessor.Domain.Documents.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Application.Documents.Commands.ProcessDocument;

public sealed class ProcessDocumentCommandHandler(
    IDocumentRepository documentRepository,
    IDocumentAnalyzer documentAnalyzer,
    TimeProvider timeProvider,
    ILogger<ProcessDocumentCommandHandler> logger)
    : IRequestHandler<ProcessDocumentCommand, Result<DocumentResponse>>
{
    public async Task<Result<DocumentResponse>> Handle(
        ProcessDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sha256 = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();

        var document = Document.Create(
            request.FileName,
            request.MediaType,
            request.Content.LongLength,
            sha256,
            now);

        document.StartProcessing(now);

        await documentRepository.AddAsync(document, cancellationToken);
        await documentRepository.SaveChangesAsync(cancellationToken);

        ProviderProcessingMetrics? providerMetrics = null;
        try
        {
            var providerResult = await documentAnalyzer.AnalyzeAsync(
                new DocumentAnalyzerInput(
                    request.MediaType,
                    request.Content),
                cancellationToken);
            providerMetrics = providerResult.Metrics;

            var analysis = DocumentAnalysisPolicy.Build(providerResult.Analysis);
            var metrics = new DocumentProcessingMetadata(
                providerResult.Metrics.Provider,
                providerResult.Metrics.Model,
                providerResult.Metrics.LatencyMs,
                providerResult.Metrics.InputTokens,
                providerResult.Metrics.OutputTokens,
                providerResult.Metrics.EstimatedCostUsd,
                providerResult.Metrics.CachedInputTokens,
                providerResult.Metrics.CacheWriteTokens);

            cancellationToken.ThrowIfCancellationRequested();
            document.Complete(analysis, metrics, timeProvider.GetUtcNow());
            await PersistAsync(document);

            logger.LogInformation(
                "Document processed documentId={DocumentId} type={DocumentType} requiresReview={RequiresReview} model={Model} latencyMs={LatencyMs}",
                document.Id,
                analysis.DocumentType,
                analysis.RequiresReview,
                metrics.Model,
                metrics.LatencyMs);

            return Result<DocumentResponse>.Success(DocumentResponse.FromDomain(document));
        }
        catch (DocumentAnalysisException exception)
        {
            logger.LogWarning(
                "Document analysis failed documentId={DocumentId} failureKind={FailureKind}",
                document.Id,
                exception.FailureKind);

            document.Fail(
                "The AI provider could not analyze the document.",
                timeProvider.GetUtcNow(),
                ToDomainMetrics(exception.Metrics ?? providerMetrics));

            await PersistAsync(document);

            return Result<DocumentResponse>.Failure(
                Error.External(
                    $"document.analysis_{ToErrorCode(exception.FailureKind)}",
                    "The document could not be analyzed. The failed attempt was recorded.",
                    document.Id));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordCancellationAsync(document, providerMetrics);
            throw;
        }
    }

    private static DocumentProcessingMetadata? ToDomainMetrics(ProviderProcessingMetrics? metrics) =>
        metrics is null
            ? null
            : new DocumentProcessingMetadata(
                metrics.Provider,
                metrics.Model,
                metrics.LatencyMs,
                metrics.InputTokens,
                metrics.OutputTokens,
                metrics.EstimatedCostUsd,
                metrics.CachedInputTokens,
                metrics.CacheWriteTokens);

    private static string ToErrorCode(DocumentAnalysisFailureKind failureKind) => failureKind switch
    {
        DocumentAnalysisFailureKind.InvalidOutput => "invalid_output",
        DocumentAnalysisFailureKind.Refused => "refused",
        DocumentAnalysisFailureKind.Incomplete => "incomplete",
        DocumentAnalysisFailureKind.Authentication => "authentication",
        DocumentAnalysisFailureKind.RateLimited => "rate_limited",
        DocumentAnalysisFailureKind.RequestRejected => "request_rejected",
        DocumentAnalysisFailureKind.ProviderUnavailable => "provider_unavailable",
        DocumentAnalysisFailureKind.Timeout => "timeout",
        _ => "unknown",
    };

    private async Task RecordCancellationAsync(
        Document document,
        ProviderProcessingMetrics? providerMetrics)
    {
        document.Fail(
            "Document processing was cancelled before completion.",
            timeProvider.GetUtcNow(),
            ToDomainMetrics(providerMetrics));

        try
        {
            await PersistAsync(document);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Failed to persist cancelled document documentId={DocumentId} exceptionType={ExceptionType}",
                document.Id,
                exception.GetType().Name);
        }
    }

    private async Task PersistAsync(Document document)
    {
        using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await documentRepository.UpdateAsync(document, saveTimeout.Token);
        await documentRepository.SaveChangesAsync(saveTimeout.Token);
    }
}
