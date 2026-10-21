using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Ports;

public interface IDocumentAnalysisProviderFactory
{
    IDocumentAnalysisProvider Get(DocumentAnalysisProviderType providerType);
}
