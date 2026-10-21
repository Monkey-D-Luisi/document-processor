using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Tests.Documents.Analysis;

public sealed class DocumentAnalysisProviderTypeCodecTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData(" OpenAI ")]
    public void Parse_accepts_openai_stable_id(string providerId)
    {
        Assert.Equal(DocumentAnalysisProviderType.OpenAi, DocumentAnalysisProviderTypeCodec.Parse(providerId));
        Assert.Equal("openai", DocumentAnalysisProviderTypeCodec.ToId(DocumentAnalysisProviderType.OpenAi));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anthropic")]
    public void TryParse_rejects_unknown_or_empty_ids(string? providerId)
    {
        Assert.False(DocumentAnalysisProviderTypeCodec.TryParse(providerId, out _));
    }
}
