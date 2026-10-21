using DocumentProcessor.Application.Common.Models;

namespace DocumentProcessor.Application.Common.Interfaces;

public interface IDocumentAnalysisProvider
{
    DocumentAnalysisProviderType ProviderType { get; }

    Task<DocumentAnalyzerResult> AnalyzeAsync(
        DocumentAnalyzerInput input,
        CancellationToken cancellationToken = default);
}
