#pragma warning disable OPENAI001

using OpenAI.Responses;

namespace DocumentProcessor.Infrastructure.AI;

internal interface IOpenAiResponsesClient
{
    Task<OpenAiResponsesResult> CreateResponseAsync(
        CreateResponseOptions options,
        CancellationToken cancellationToken);
}

internal sealed record OpenAiResponsesResult(
    string OutputText,
    string? Model,
    string? Status,
    int? InputTokens,
    int? OutputTokens,
    int? CachedInputTokens,
    int? CacheWriteTokens);

internal sealed class OpenAiResponsesClient(ResponsesClient client)
    : IOpenAiResponsesClient
{
    public async Task<OpenAiResponsesResult> CreateResponseAsync(
        CreateResponseOptions options,
        CancellationToken cancellationToken)
    {
        var result = await client.CreateResponseAsync(options, cancellationToken);
        var response = result.Value;

        return new OpenAiResponsesResult(
            response.GetOutputText(),
            response.Model,
            response.Status?.ToString(),
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount,
            response.Usage?.InputTokenDetails?.CachedTokenCount,
            response.Usage?.InputTokenDetails?.CacheWriteTokenCount);
    }
}
