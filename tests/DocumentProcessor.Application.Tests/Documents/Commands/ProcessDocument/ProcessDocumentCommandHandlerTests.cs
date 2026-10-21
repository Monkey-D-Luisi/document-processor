using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Documents.Analysis.Exceptions;
using DocumentProcessor.Application.Documents.Commands.ProcessDocument;
using DocumentProcessor.Application.Documents.Analysis.Models;
using DocumentProcessor.Domain.Documents;
using DocumentProcessor.Domain.Documents.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DocumentProcessor.Application.Tests.Documents.Commands.ProcessDocument;

public sealed class ProcessDocumentCommandHandlerTests
{
    [Fact]
    public async Task Successful_analysis_completes_and_persists_document()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var analyzer = Substitute.For<IDocumentAnalyzer>();
        var clock = new FixedTimeProvider(
            DateTimeOffset.Parse("2026-08-25T12:00:00Z"));

        analyzer.AnalyzeAsync(
                Arg.Any<DocumentAnalyzerInput>(),
                Arg.Any<CancellationToken>())
            .Returns(new DocumentAnalyzerResult(
                CreateCandidate(),
                new ProviderProcessingMetrics(
                    "OpenAI",
                    "test-model",
                    120,
                    1000,
                    200,
                    0.001m)));

        var handler = new ProcessDocumentCommandHandler(
            repository,
            analyzer,
            clock,
            NullLogger<ProcessDocumentCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDocumentCommand(
                "invoice.pdf",
                "application/pdf",
                "%PDF-1.7 test"u8.ToArray()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Completed, result.Value!.Status);
        Assert.False(result.Value.Analysis!.RequiresReview);

        await repository.Received(1)
            .AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());

        await repository.Received(1)
            .UpdateAsync(
                Arg.Is<Document>(document => document.Status == DocumentStatus.Completed),
                Arg.Any<CancellationToken>());

        await repository.Received(2)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Analysis_failure_persists_failure_metrics_and_document_id()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var analyzer = Substitute.For<IDocumentAnalyzer>();
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
        Document? updatedDocument = null;
        repository.UpdateAsync(
                Arg.Do<Document>(document => updatedDocument = document),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        analyzer.AnalyzeAsync(Arg.Any<DocumentAnalyzerInput>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<DocumentAnalyzerResult>(new DocumentAnalysisException(
                "Provider rate limit.",
                DocumentAnalysisFailureKind.RateLimited,
                new ProviderProcessingMetrics("OpenAI", "gpt-5.6-luna", 321, 100, 25, 0.0001m, 10))));

        var handler = new ProcessDocumentCommandHandler(
            repository,
            analyzer,
            clock,
            NullLogger<ProcessDocumentCommandHandler>.Instance);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("document.analysis_rate_limited", result.Error!.Code);
        Assert.Equal(updatedDocument!.Id, result.Error.ResourceId);
        Assert.Equal(DocumentStatus.Failed, updatedDocument.Status);
        Assert.Equal(10, updatedDocument.ProcessingMetadata!.CachedInputTokens);
        Assert.Equal(321, updatedDocument.ProcessingMetadata.LatencyMs);
        await repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_analysis_marks_persisted_document_failed_before_rethrowing()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var analyzer = Substitute.For<IDocumentAnalyzer>();
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
        Document? updatedDocument = null;
        repository.UpdateAsync(
                Arg.Do<Document>(document => updatedDocument = document),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        analyzer.AnalyzeAsync(Arg.Any<DocumentAnalyzerInput>(), cancellation.Token)
            .Returns(_ => Task.FromCanceled<DocumentAnalyzerResult>(cancellation.Token));

        var handler = new ProcessDocumentCommandHandler(
            repository,
            analyzer,
            clock,
            NullLogger<ProcessDocumentCommandHandler>.Instance);

        var act = () => handler.Handle(CreateCommand(), cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.Equal(DocumentStatus.Failed, updatedDocument!.Status);
        await repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancellation_after_analysis_but_before_completion_does_not_commit_result()
    {
        var repository = Substitute.For<IDocumentRepository>();
        var analyzer = Substitute.For<IDocumentAnalyzer>();
        var clock = new FixedTimeProvider(DateTimeOffset.Parse("2026-08-25T12:00:00Z"));
        Document? updatedDocument = null;
        repository.UpdateAsync(
                Arg.Do<Document>(document => updatedDocument = document),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        using var cancellation = new CancellationTokenSource();
        var providerResult = new DocumentAnalyzerResult(
            CreateCandidate(),
            new ProviderProcessingMetrics("OpenAI", "gpt-5.6-luna", 20, 100, 20, 0.001m));
        analyzer.AnalyzeAsync(Arg.Any<DocumentAnalyzerInput>(), cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(providerResult);
            });

        var handler = new ProcessDocumentCommandHandler(
            repository,
            analyzer,
            clock,
            NullLogger<ProcessDocumentCommandHandler>.Instance);

        var act = () => handler.Handle(CreateCommand(), cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.Equal(DocumentStatus.Failed, updatedDocument!.Status);
        Assert.Null(updatedDocument.Analysis);
        Assert.Equal("gpt-5.6-luna", updatedDocument.ProcessingMetadata!.Model);
        await repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ProcessDocumentCommand CreateCommand() =>
        new("invoice.pdf", "application/pdf", "%PDF-1.7 test"u8.ToArray());

    private static DocumentAnalysisCandidate CreateCandidate() =>
        new(
            "invoice",
            "Example SL",
            "B12345678",
            "Customer SL",
            "B87654321",
            "F-001",
            "2026-08-25",
            null,
            "EUR",
            100m,
            21m,
            121m,
            [new DocumentLineCandidate("Service", 1m, 100m, 21m, 100m, TaxAmount: 21m)],
            [],
            [])
        {
            VatBreakdown = [new DocumentTaxBreakdownCandidate(100m, 21m, 21m)],
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
