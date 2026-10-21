using DocumentProcessor.Application.Documents.Analysis.Exceptions;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Application.Documents.Analysis.Services;
using DocumentProcessor.Application.Documents.Analysis.Models;
using NSubstitute;

namespace DocumentProcessor.Application.Tests.Documents.Analysis;

public sealed class DocumentAnalysisProviderFactoryTests
{
    [Fact]
    public void Get_returns_provider_for_requested_identity()
    {
        var provider = Substitute.For<IDocumentAnalysisProvider>();
        provider.ProviderType.Returns(DocumentAnalysisProviderType.OpenAi);
        var factory = new DocumentAnalysisProviderFactory([provider]);

        Assert.Same(provider, factory.Get(DocumentAnalysisProviderType.OpenAi));
    }

    [Fact]
    public void Get_rejects_an_unregistered_provider_identity()
    {
        var factory = new DocumentAnalysisProviderFactory([]);

        var exception = Assert.Throws<DocumentAnalysisException>(
            () => factory.Get((DocumentAnalysisProviderType)99));

        Assert.Equal(DocumentAnalysisFailureKind.ProviderUnavailable, exception.FailureKind);
    }

    [Fact]
    public void Constructor_rejects_duplicate_provider_identities()
    {
        var first = Substitute.For<IDocumentAnalysisProvider>();
        var second = Substitute.For<IDocumentAnalysisProvider>();
        first.ProviderType.Returns(DocumentAnalysisProviderType.OpenAi);
        second.ProviderType.Returns(DocumentAnalysisProviderType.OpenAi);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new DocumentAnalysisProviderFactory([first, second]));

        Assert.Contains("openai", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_analyzer_delegates_to_provider_selected_by_typed_identity()
    {
        var providerFactory = Substitute.For<IDocumentAnalysisProviderFactory>();
        var provider = Substitute.For<IDocumentAnalysisProvider>();
        var input = new DocumentAnalyzerInput("application/pdf", [1, 2, 3]);
        var cancellationToken = new CancellationToken(canceled: true);
        var expected = CreateResult();
        providerFactory.Get(DocumentAnalysisProviderType.OpenAi).Returns(provider);
        provider.AnalyzeAsync(input, cancellationToken).Returns(Task.FromResult(expected));
        var analyzer = new ConfiguredDocumentAnalyzer(providerFactory, DocumentAnalysisProviderType.OpenAi);

        var result = await analyzer.AnalyzeAsync(input, cancellationToken);

        Assert.Same(expected, result);
        providerFactory.Received(1).Get(DocumentAnalysisProviderType.OpenAi);
        await provider.Received(1).AnalyzeAsync(input, cancellationToken);
    }

    private static DocumentAnalyzerResult CreateResult() =>
        new(
            new DocumentAnalysisCandidate(
                "unknown", null, null, null, null, null, null, null, null,
                null, null, null, [], [], []),
            new ProviderProcessingMetrics("OpenAI", "gpt-6-luna", 1, 1, 1, 0.0001m));
}
