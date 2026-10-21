using DocumentProcessor.Infrastructure.AI;
using FluentAssertions;
using OpenAI.Responses;

namespace DocumentProcessor.Infrastructure.Tests.AI;

public sealed class DocumentAnalysisPromptFactoryTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void Includes_media_type_when_creating_image_input(string mediaType)
    {
        var options = DocumentAnalysisPromptFactory.Create(
            new OpenAiDocumentOptions(),
            mediaType,
            [1, 2, 3]);

        var message = options.InputItems.OfType<MessageResponseItem>().Single();

        message.Content.Should().HaveCount(2);
        message.Content[1].InputImageUri.Should().StartWith($"data:{mediaType};base64,");
    }
}
