using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Application.Documents.Analysis.Exceptions;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Services;

[IoC(typeof(IDocumentAnalysisProviderFactory), IoCLifetime.Scoped)]
public sealed class DocumentAnalysisProviderFactory : IDocumentAnalysisProviderFactory
{
    private readonly IReadOnlyDictionary<DocumentAnalysisProviderType, IDocumentAnalysisProvider> _providers;

    public DocumentAnalysisProviderFactory(IEnumerable<IDocumentAnalysisProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var implementations = providers
            .OrderBy(provider => provider.GetType().FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (var provider in implementations)
            _ = DocumentAnalysisProviderTypeCodec.ToId(provider.ProviderType);

        var duplicate = implementations
            .GroupBy(provider => provider.ProviderType)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            var providerId = DocumentAnalysisProviderTypeCodec.ToId(duplicate.Key);
            var implementationsForProvider = string.Join(
                ", ",
                duplicate.Select(provider => provider.GetType().FullName));
            throw new InvalidOperationException(
                $"Duplicate document analysis provider '{providerId}': {implementationsForProvider}");
        }

        _providers = implementations.ToDictionary(provider => provider.ProviderType);
    }

    public IDocumentAnalysisProvider Get(DocumentAnalysisProviderType providerType) =>
        _providers.TryGetValue(providerType, out var provider)
            ? provider
            : throw new DocumentAnalysisException(
                "The configured document analysis provider is not supported.",
                DocumentAnalysisFailureKind.ProviderUnavailable);
}
