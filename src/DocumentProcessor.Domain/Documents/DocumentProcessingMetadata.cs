namespace DocumentProcessor.Domain.Documents;

public sealed record DocumentProcessingMetadata(
    string Provider,
    string Model,
    long LatencyMs,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCostUsd,
    int? CachedInputTokens = null,
    int? CacheWriteTokens = null);
