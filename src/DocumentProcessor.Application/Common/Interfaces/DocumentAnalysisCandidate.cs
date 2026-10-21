namespace DocumentProcessor.Application.Common.Interfaces;

public sealed record DocumentAnalysisCandidate(
    string DocumentType,
    string? SupplierName,
    string? SupplierTaxId,
    string? CustomerName,
    string? CustomerTaxId,
    string? DocumentNumber,
    string? IssueDate,
    string? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? TaxTotal,
    decimal? Total,
    IReadOnlyList<DocumentLineCandidate?> Lines,
    IReadOnlyList<string> UncertainFields,
    IReadOnlyList<string> Warnings)
{
    public DocumentAdjustmentCandidate? HeaderDiscount { get; init; }
    public IReadOnlyList<DocumentTaxBreakdownCandidate?> VatBreakdown { get; init; } = [];
    public IReadOnlyList<DocumentTaxBreakdownCandidate?> Surcharges { get; init; } = [];
    public IReadOnlyList<DocumentTaxBreakdownCandidate?> Withholdings { get; init; } = [];
}

public sealed record DocumentLineCandidate(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TaxRate,
    decimal? LineTotal,
    DocumentAdjustmentCandidate? Discount = null,
    decimal? TaxAmount = null);

public sealed record DocumentAdjustmentCandidate(decimal? Rate, decimal? Amount);

public sealed record DocumentTaxBreakdownCandidate(decimal? Base, decimal? Rate, decimal? Amount);
