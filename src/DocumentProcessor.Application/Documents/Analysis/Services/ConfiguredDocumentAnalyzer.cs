using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Services;

[IoC(typeof(IDocumentAnalyzer), IoCLifetime.Scoped)]
public sealed class ConfiguredDocumentAnalyzer(
    IDocumentAnalysisProviderFactory providerFactory,
    DocumentAnalysisProviderType providerType) : IDocumentAnalyzer
{
    public Task<DocumentAnalyzerResult> AnalyzeAsync(
        DocumentAnalyzerInput input,
        CancellationToken cancellationToken = default) =>
        providerFactory.Get(providerType).AnalyzeAsync(input, cancellationToken);
}
