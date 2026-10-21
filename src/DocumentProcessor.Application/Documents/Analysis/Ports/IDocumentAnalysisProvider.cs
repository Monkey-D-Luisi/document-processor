using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Ports;

public interface IDocumentAnalysisProvider
{
    DocumentAnalysisProviderType ProviderType { get; }

    Task<DocumentAnalyzerResult> AnalyzeAsync(
        DocumentAnalyzerInput input,
        CancellationToken cancellationToken = default);
}
