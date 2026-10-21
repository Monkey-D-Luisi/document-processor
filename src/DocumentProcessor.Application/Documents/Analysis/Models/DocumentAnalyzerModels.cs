namespace DocumentProcessor.Application.Documents.Analysis.Models;

public sealed record DocumentAnalyzerInput(
    string MediaType,
    byte[] Content);

public sealed record DocumentAnalyzerResult(
    DocumentAnalysisCandidate Analysis,
    ProviderProcessingMetrics Metrics);

public sealed record ProviderProcessingMetrics(
    string Provider,
    string Model,
    long LatencyMs,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCostUsd,
    int? CachedInputTokens = null,
    int? CacheWriteTokens = null);
