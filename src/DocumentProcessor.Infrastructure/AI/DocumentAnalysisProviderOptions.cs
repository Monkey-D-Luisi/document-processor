namespace DocumentProcessor.Infrastructure.AI;

internal sealed class DocumentAnalysisProviderOptions
{
    public const string SectionName = "DocumentAnalysis";

    public string Provider { get; init; } = "openai";
}
