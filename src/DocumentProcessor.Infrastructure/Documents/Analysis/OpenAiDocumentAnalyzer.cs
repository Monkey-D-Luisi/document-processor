using System.Diagnostics;
using DocumentProcessor.Application.Documents.Analysis.Exceptions;
using DocumentProcessor.Application.Documents.Analysis.Ports;
using DocumentProcessor.Application.Common.DependencyInjection;
using DocumentProcessor.Application.Documents.Analysis.Models;
using System.ClientModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Infrastructure.Documents.Analysis;

[IoC(typeof(IDocumentAnalysisProvider), IoCLifetime.Scoped, allowMultiple: true)]
internal sealed class OpenAiDocumentAnalyzer(
    IOpenAiResponsesClient responsesClient,
    IOptions<OpenAiDocumentOptions> options,
    ILogger<OpenAiDocumentAnalyzer> logger)
    : IDocumentAnalysisProvider
{
    public DocumentAnalysisProviderType ProviderType => DocumentAnalysisProviderType.OpenAi;

    public async Task<DocumentAnalyzerResult> AnalyzeAsync(
        DocumentAnalyzerInput input,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var request = DocumentAnalysisPromptFactory.Create(
                configuration,
                input.MediaType,
                input.Content);

            var response = await responsesClient.CreateResponseAsync(request, cancellationToken);
            stopwatch.Stop();
            var model = response.Model ?? configuration.Model;
            var metrics = new ProviderProcessingMetrics(
                "OpenAI",
                model,
                stopwatch.ElapsedMilliseconds,
                response.InputTokens,
                response.OutputTokens,
                EstimateCost(
                    model,
                    response.InputTokens,
                    response.OutputTokens,
                    response.CachedInputTokens,
                    response.CacheWriteTokens,
                    configuration),
                response.CachedInputTokens,
                response.CacheWriteTokens);

            if (!string.Equals(response.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentAnalysisException(
                    "OpenAI did not complete the document analysis.",
                    DocumentAnalysisFailureKind.Incomplete,
                    metrics);
            }

            DocumentAnalysisCandidate analysis;
            try
            {
                analysis = DocumentAnalysisResponseParser.Parse(response.OutputText);
            }
            catch (DocumentAnalysisException exception)
            {
                throw new DocumentAnalysisException(
                    exception.Message,
                    DocumentAnalysisFailureKind.InvalidOutput,
                    metrics,
                    exception);
            }

            logger.LogInformation(
                "OpenAI document analysis completed model={Model} mediaType={MediaType} sizeBytes={SizeBytes} latencyMs={LatencyMs} inputTokens={InputTokens} cachedInputTokens={CachedInputTokens} cacheWriteTokens={CacheWriteTokens} outputTokens={OutputTokens} estimatedCostUsd={EstimatedCostUsd}",
                model,
                input.MediaType,
                input.Content.LongLength,
                stopwatch.ElapsedMilliseconds,
                response.InputTokens,
                response.CachedInputTokens,
                response.CacheWriteTokens,
                response.OutputTokens,
                metrics.EstimatedCostUsd);

            return new DocumentAnalyzerResult(
                analysis,
                metrics);
        }
        catch (DocumentAnalysisException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI analysis failed failureKind={FailureKind} latencyMs={LatencyMs} inputTokens={InputTokens} outputTokens={OutputTokens}",
                exception.FailureKind,
                stopwatch.ElapsedMilliseconds,
                exception.Metrics?.InputTokens,
                exception.Metrics?.OutputTokens);

            throw;
        }
        catch (ClientResultException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI request failed statusCode={StatusCode} latencyMs={LatencyMs}",
                exception.Status,
                stopwatch.ElapsedMilliseconds);

            throw new DocumentAnalysisException(
                "OpenAI could not analyze the document.",
                exception.Status switch
                {
                    401 or 403 => DocumentAnalysisFailureKind.Authentication,
                    429 => DocumentAnalysisFailureKind.RateLimited,
                    408 => DocumentAnalysisFailureKind.Timeout,
                    >= 500 => DocumentAnalysisFailureKind.ProviderUnavailable,
                    400 or 404 or 413 => DocumentAnalysisFailureKind.RequestRejected,
                    _ => DocumentAnalysisFailureKind.Unknown,
                },
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI connection failed latencyMs={LatencyMs}",
                stopwatch.ElapsedMilliseconds);

            throw new DocumentAnalysisException(
                "OpenAI could not be reached.",
                DocumentAnalysisFailureKind.ProviderUnavailable,
                innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI request timed out latencyMs={LatencyMs}",
                stopwatch.ElapsedMilliseconds);

            throw new DocumentAnalysisException(
                "OpenAI did not respond before the request timeout.",
                DocumentAnalysisFailureKind.Timeout,
                innerException: exception);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "OpenAI request failed exceptionType={ExceptionType} latencyMs={LatencyMs}",
                exception.GetType().Name,
                stopwatch.ElapsedMilliseconds);

            throw new DocumentAnalysisException(
                "OpenAI could not analyze the document.",
                DocumentAnalysisFailureKind.Unknown,
                innerException: exception);
        }
    }

    private static decimal? EstimateCost(
        string model,
        int? inputTokens,
        int? outputTokens,
        int? cachedInputTokens,
        int? cacheWriteTokens,
        OpenAiDocumentOptions configuration)
    {
        if (inputTokens is null || outputTokens is null ||
            cachedInputTokens is < 0 || cachedInputTokens > inputTokens ||
            cacheWriteTokens is < 0 || cacheWriteTokens > inputTokens - (cachedInputTokens ?? 0) ||
            !configuration.ModelPricing.TryGetValue(model, out var pricing))
            return null;

        var useLongContextPrices = inputTokens > pricing.LongContextThresholdTokens;
        var inputPrice = useLongContextPrices
            ? pricing.LongContextInputCostPerMillionTokensUsd
            : pricing.InputCostPerMillionTokensUsd;
        var cachedInputPrice = useLongContextPrices
            ? pricing.LongContextCachedInputCostPerMillionTokensUsd
            : pricing.CachedInputCostPerMillionTokensUsd;
        var outputPrice = useLongContextPrices
            ? pricing.LongContextOutputCostPerMillionTokensUsd
            : pricing.OutputCostPerMillionTokensUsd;

        var cached = cachedInputTokens ?? 0;
        var cacheWrite = cacheWriteTokens ?? 0;
        // GPT-5.6 and later price written cache tokens at 1.25x uncached input.
        var input = (inputTokens.Value - cached - cacheWrite) / 1_000_000m * inputPrice +
                    cached / 1_000_000m * cachedInputPrice +
                    cacheWrite / 1_000_000m * inputPrice * 1.25m;
        var output = outputTokens.Value / 1_000_000m * outputPrice;

        return decimal.Round(input + output, 6, MidpointRounding.AwayFromZero);
    }
}
