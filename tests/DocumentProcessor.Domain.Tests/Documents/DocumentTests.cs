using DocumentProcessor.Domain.Documents;

namespace DocumentProcessor.Domain.Tests.Documents;

public sealed class DocumentTests
{
    [Fact]
    public void Complete_requires_processing_state()
    {
        var now = DateTimeOffset.Parse("2026-08-25T12:00:00Z");
        var document = Document.Create(
            "invoice.pdf",
            "application/pdf",
            100,
            new string('a', 64),
            now);

        var act = () => document.Complete(
            CreateAnalysis(),
            CreateMetrics(),
            now.AddSeconds(1));

        Assert.ThrowsAny<InvalidOperationException>(act);
    }

    [Fact]
    public void Processing_document_can_be_completed()
    {
        var now = DateTimeOffset.Parse("2026-08-25T12:00:00Z");
        var document = Document.Create(
            "invoice.pdf",
            "application/pdf",
            100,
            new string('a', 64),
            now);

        document.StartProcessing(now);
        document.Complete(CreateAnalysis(), CreateMetrics(), now.AddSeconds(1));

        Assert.Equal(DocumentStatus.Completed, document.Status);
        Assert.NotNull(document.Analysis);
        Assert.NotNull(document.ProcessingMetadata);
        Assert.Null(document.FailureReason);
    }

    private static DocumentAnalysis CreateAnalysis() =>
        new(
            DocumentType.Invoice,
            "Example SL",
            "B12345678",
            null,
            null,
            "F-001",
            new DateOnly(2026, 8, 25),
            null,
            "EUR",
            100m,
            21m,
            121m,
            [],
            []);

    private static DocumentProcessingMetadata CreateMetrics() =>
        new("OpenAI", "test-model", 10, 100, 50, 0.001m);
}
