using DocumentProcessor.Application.Common.Models;

namespace DocumentProcessor.Application.Common.Interfaces;

public interface IDocumentAnalysisProviderFactory
{
    IDocumentAnalysisProvider Get(DocumentAnalysisProviderType providerType);
}
