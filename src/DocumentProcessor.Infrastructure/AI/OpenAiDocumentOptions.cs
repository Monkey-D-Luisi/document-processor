namespace DocumentProcessor.Infrastructure.AI;

internal sealed class OpenAiDocumentOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gpt-6-luna";
    public int MaxOutputTokenCount { get; init; } = 5000;
    public Dictionary<string, OpenAiModelPricing> ModelPricing { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class OpenAiModelPricing
{
    public decimal InputCostPerMillionTokensUsd { get; init; }
    public decimal CachedInputCostPerMillionTokensUsd { get; init; }
    public decimal OutputCostPerMillionTokensUsd { get; init; }
    public int LongContextThresholdTokens { get; init; } = 272_000;
    public decimal LongContextInputCostPerMillionTokensUsd { get; init; }
    public decimal LongContextCachedInputCostPerMillionTokensUsd { get; init; }
    public decimal LongContextOutputCostPerMillionTokensUsd { get; init; }
}
