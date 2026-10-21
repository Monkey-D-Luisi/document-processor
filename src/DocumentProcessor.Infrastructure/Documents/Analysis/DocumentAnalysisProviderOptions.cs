namespace DocumentProcessor.Infrastructure.Documents.Analysis;

internal sealed class DocumentAnalysisProviderOptions
{
    public const string SectionName = "DocumentAnalysis";

    public string Provider { get; init; } = "openai";
}
