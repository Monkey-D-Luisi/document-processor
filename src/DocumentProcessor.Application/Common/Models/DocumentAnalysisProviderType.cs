namespace DocumentProcessor.Application.Common.Models;

public enum DocumentAnalysisProviderType
{
    OpenAi = 1,
}

public static class DocumentAnalysisProviderTypeCodec
{
    public static string ToId(DocumentAnalysisProviderType providerType) => providerType switch
    {
        DocumentAnalysisProviderType.OpenAi => "openai",
        _ => throw new ArgumentOutOfRangeException(
            nameof(providerType),
            providerType,
            "The document analysis provider is not supported."),
    };

    public static bool TryParse(string? providerId, out DocumentAnalysisProviderType providerType)
    {
        providerType = default;
        if (!string.Equals(providerId?.Trim(), "openai", StringComparison.OrdinalIgnoreCase))
            return false;

        providerType = DocumentAnalysisProviderType.OpenAi;
        return true;
    }

    public static DocumentAnalysisProviderType Parse(string? providerId) =>
        TryParse(providerId, out var providerType)
            ? providerType
            : throw new ArgumentException(
                "The configured document analysis provider is not supported.",
                nameof(providerId));
}
