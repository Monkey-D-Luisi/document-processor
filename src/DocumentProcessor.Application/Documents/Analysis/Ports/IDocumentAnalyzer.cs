using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Ports;

public interface IDocumentAnalyzer
{
    Task<DocumentAnalyzerResult> AnalyzeAsync(
        DocumentAnalyzerInput input,
        CancellationToken cancellationToken = default);
}
