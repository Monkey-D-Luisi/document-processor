using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Application.Common.Interfaces;
using DocumentProcessor.Application.Common.Models;

namespace DocumentProcessor.Application.Documents.Services;

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
