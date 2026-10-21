using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessor.Application.Common.Exceptions;
using DocumentProcessor.Application.Common.Interfaces;

namespace DocumentProcessor.Infrastructure.AI;

internal static class DocumentAnalysisResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static DocumentAnalysisCandidate Parse(string json)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ResponseContract>(json, JsonOptions)
                ?? throw new DocumentAnalysisException("The model returned an empty analysis.");

            if (string.IsNullOrWhiteSpace(response.DocumentType) ||
                response.Lines is not { Count: <= 100 } lines ||
                response.UncertainFields is not { Count: <= 50 } uncertainFields ||
                response.Warnings is not { Count: <= 50 } warnings ||
                response.VatBreakdown is not { Count: <= 50 } vatBreakdown ||
                response.Surcharges is not { Count: <= 50 } surcharges ||
                response.Withholdings is not { Count: <= 50 } withholdings ||
                lines.Any(line => line is null) ||
                uncertainFields.Any(value => value is null) ||
                warnings.Any(value => value is null) ||
                vatBreakdown.Any(value => value is null) ||
                surcharges.Any(value => value is null) ||
                withholdings.Any(value => value is null))
                throw new DocumentAnalysisException("The model returned an empty analysis.");

            return new DocumentAnalysisCandidate(
                response.DocumentType,
                response.SupplierName,
                response.SupplierTaxId,
                response.CustomerName,
                response.CustomerTaxId,
                response.DocumentNumber,
                response.IssueDate,
                response.DueDate,
                response.Currency,
                response.Subtotal,
                response.TaxTotal,
                response.Total,
                lines
                    .Select(line => new DocumentLineCandidate(
                        line!.Description,
                        line.Quantity,
                        line.UnitPrice,
                        line.TaxRate,
                        line.LineTotal,
                        ToCandidate(line.Discount),
                        line.TaxAmount))
                    .ToArray(),
                uncertainFields.Select(value => value!).ToArray(),
                warnings.Select(value => value!).ToArray())
            {
                HeaderDiscount = ToCandidate(response.HeaderDiscount),
                VatBreakdown = ToCandidate(vatBreakdown),
                Surcharges = ToCandidate(surcharges),
                Withholdings = ToCandidate(withholdings),
            };
        }
        catch (JsonException ex)
        {
            throw new DocumentAnalysisException(
                "The model returned invalid structured output.",
                ex);
        }
    }

    private static DocumentAdjustmentCandidate? ToCandidate(AdjustmentContract? adjustment) =>
        adjustment is null ? null : new DocumentAdjustmentCandidate(adjustment.Rate, adjustment.Amount);

    private static IReadOnlyList<DocumentTaxBreakdownCandidate?> ToCandidate(
        IReadOnlyList<TaxBreakdownContract?> values) =>
        values.Select(value => value is null
            ? null
            : new DocumentTaxBreakdownCandidate(value.Base, value.Rate, value.Amount)).ToArray();

    private sealed class ResponseContract
    {
        [JsonRequired]
        public string DocumentType { get; set; } = string.Empty;
        [JsonRequired]
        public string? SupplierName { get; set; }
        [JsonRequired]
        public string? SupplierTaxId { get; set; }
        [JsonRequired]
        public string? CustomerName { get; set; }
        [JsonRequired]
        public string? CustomerTaxId { get; set; }
        [JsonRequired]
        public string? DocumentNumber { get; set; }
        [JsonRequired]
        public string? IssueDate { get; set; }
        [JsonRequired]
        public string? DueDate { get; set; }
        [JsonRequired]
        public string? Currency { get; set; }
        [JsonRequired]
        public decimal? Subtotal { get; set; }
        [JsonRequired]
        public decimal? TaxTotal { get; set; }
        [JsonRequired]
        public decimal? Total { get; set; }
        [JsonRequired]
        public List<LineContract?>? Lines { get; set; }
        [JsonRequired]
        public List<string?>? UncertainFields { get; set; }
        [JsonRequired]
        public List<string?>? Warnings { get; set; }
        [JsonRequired]
        public AdjustmentContract? HeaderDiscount { get; set; }
        [JsonRequired]
        public List<TaxBreakdownContract?>? VatBreakdown { get; set; }
        [JsonRequired]
        public List<TaxBreakdownContract?>? Surcharges { get; set; }
        [JsonRequired]
        public List<TaxBreakdownContract?>? Withholdings { get; set; }
    }

    private sealed class LineContract
    {
        [JsonRequired]
        public string Description { get; set; } = string.Empty;
        [JsonRequired]
        public decimal? Quantity { get; set; }
        [JsonRequired]
        public decimal? UnitPrice { get; set; }
        [JsonRequired]
        public decimal? TaxRate { get; set; }
        [JsonRequired]
        public decimal? LineTotal { get; set; }
        [JsonRequired]
        public AdjustmentContract? Discount { get; set; }
        [JsonRequired]
        public decimal? TaxAmount { get; set; }
    }

    private sealed class AdjustmentContract
    {
        [JsonRequired]
        public decimal? Rate { get; set; }
        [JsonRequired]
        public decimal? Amount { get; set; }
    }

    private sealed class TaxBreakdownContract
    {
        [JsonRequired]
        public decimal? Base { get; set; }
        [JsonRequired]
        public decimal? Rate { get; set; }
        [JsonRequired]
        public decimal? Amount { get; set; }
    }
}
