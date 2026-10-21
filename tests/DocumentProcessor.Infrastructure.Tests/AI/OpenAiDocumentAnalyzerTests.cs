using DocumentProcessor.Application.Common.Exceptions;
using DocumentProcessor.Infrastructure.AI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Infrastructure.Tests.AI;

public sealed class OpenAiDocumentAnalyzerTests
{
    private const string ValidInvoiceJson = """
        {
          "documentType": "invoice", "supplierName": "Example SL", "supplierTaxId": null,
          "customerName": null, "customerTaxId": null, "documentNumber": "F-1",
          "issueDate": "2026-08-25", "dueDate": null, "currency": "EUR",
          "subtotal": 100, "taxTotal": 21, "total": 121, "headerDiscount": null,
          "lines": [{ "description": "Service", "quantity": 1, "unitPrice": 100,
            "taxRate": 21, "lineTotal": 100, "discount": null, "taxAmount": 21 }],
          "vatBreakdown": [{ "base": 100, "rate": 21, "amount": 21 }],
          "surcharges": [], "withholdings": [], "uncertainFields": [], "warnings": []
        }
        """;

    [Theory]
    [InlineData("gpt-5.6-luna", 0.0312)]
    [InlineData("gpt-6-luna", 0.0146)]
    public async Task Estimates_cost_using_cached_and_uncached_token_rates(
        string model,
        decimal expectedCost)
    {
        var client = new FakeResponsesClient(new OpenAiResponsesResult(
            ValidInvoiceJson,
            model,
            "Completed",
            100_000,
            10_000,
            10_000,
            20_000));
        var analyzer = CreateAnalyzer(client, model);

        var result = await analyzer.AnalyzeAsync(
            new DocumentProcessor.Application.Common.Interfaces.DocumentAnalyzerInput(
                "application/pdf",
                "%PDF-1.7"u8.ToArray()));

        result.Metrics.Model.Should().Be(model);
        result.Metrics.CachedInputTokens.Should().Be(10_000);
        result.Metrics.CacheWriteTokens.Should().Be(20_000);
        result.Metrics.EstimatedCostUsd.Should().Be(expectedCost);
    }

    [Fact]
    public async Task Estimates_cost_using_the_model_returned_by_the_provider()
    {
        var client = new FakeResponsesClient(new OpenAiResponsesResult(
            ValidInvoiceJson,
            "gpt-6-luna",
            "Completed",
            100_000,
            10_000,
            10_000,
            20_000));
        var analyzer = CreateAnalyzer(client, "gpt-5.6-luna");

        var result = await analyzer.AnalyzeAsync(
            new DocumentProcessor.Application.Common.Interfaces.DocumentAnalyzerInput(
                "application/pdf",
                "%PDF-1.7"u8.ToArray()));

        result.Metrics.Model.Should().Be("gpt-6-luna");
        result.Metrics.EstimatedCostUsd.Should().Be(0.0146m);
    }

    [Fact]
    public async Task Uses_long_context_prices_when_input_exceeds_model_threshold()
    {
        var client = new FakeResponsesClient(new OpenAiResponsesResult(
            ValidInvoiceJson,
            "gpt-6-luna",
            "Completed",
            300_000,
            10_000,
            10_000,
            20_000));
        var analyzer = CreateAnalyzer(client, "gpt-6-luna");

        var result = await analyzer.AnalyzeAsync(
            new DocumentProcessor.Application.Common.Interfaces.DocumentAnalyzerInput(
                "application/pdf",
                "%PDF-1.7"u8.ToArray()));

        result.Metrics.EstimatedCostUsd.Should().Be(0.0667m);
    }

    [Fact]
    public async Task Incomplete_provider_response_preserves_usage_metrics()
    {
        var client = new FakeResponsesClient(new OpenAiResponsesResult(
            "{}",
            "gpt-5.6-luna",
            "Incomplete",
            120,
            50,
            10,
            0));
        var analyzer = CreateAnalyzer(client, "gpt-5.6-luna");

        var act = () => analyzer.AnalyzeAsync(
            new DocumentProcessor.Application.Common.Interfaces.DocumentAnalyzerInput(
                "application/pdf",
                "%PDF-1.7"u8.ToArray()));

        var exception = await act.Should().ThrowAsync<DocumentAnalysisException>();
        exception.Which.FailureKind.Should().Be(DocumentAnalysisFailureKind.Incomplete);
        exception.Which.Metrics.Should().NotBeNull();
        exception.Which.Metrics!.InputTokens.Should().Be(120);
        exception.Which.Metrics.CacheWriteTokens.Should().Be(0);
    }

    private static OpenAiDocumentAnalyzer CreateAnalyzer(FakeResponsesClient client, string model) =>
        new(
            client,
            Options.Create(new OpenAiDocumentOptions
            {
                Model = model,
                ModelPricing = new Dictionary<string, OpenAiModelPricing>(StringComparer.OrdinalIgnoreCase)
                {
                    ["gpt-5.6-luna"] = new()
                    {
                        InputCostPerMillionTokensUsd = 0.20m,
                        CachedInputCostPerMillionTokensUsd = 0.02m,
                        OutputCostPerMillionTokensUsd = 1.20m,
                        LongContextThresholdTokens = 272_000,
                        LongContextInputCostPerMillionTokensUsd = 0.40m,
                        LongContextCachedInputCostPerMillionTokensUsd = 0.04m,
                        LongContextOutputCostPerMillionTokensUsd = 1.80m,
                    },
                    ["gpt-6-luna"] = new()
                    {
                        InputCostPerMillionTokensUsd = 0.10m,
                        CachedInputCostPerMillionTokensUsd = 0.01m,
                        OutputCostPerMillionTokensUsd = 0.50m,
                        LongContextThresholdTokens = 272_000,
                        LongContextInputCostPerMillionTokensUsd = 0.20m,
                        LongContextCachedInputCostPerMillionTokensUsd = 0.02m,
                        LongContextOutputCostPerMillionTokensUsd = 0.75m,
                    },
                },
            }),
            NullLogger<OpenAiDocumentAnalyzer>.Instance);

    private sealed class FakeResponsesClient(OpenAiResponsesResult response) : IOpenAiResponsesClient
    {
        public Task<OpenAiResponsesResult> CreateResponseAsync(
            OpenAI.Responses.CreateResponseOptions options,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
