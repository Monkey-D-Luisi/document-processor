namespace DocumentProcessor.Domain.Documents;

public sealed record DocumentAnalysis(
    DocumentType DocumentType,
    string? SupplierName,
    string? SupplierTaxId,
    string? CustomerName,
    string? CustomerTaxId,
    string? DocumentNumber,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? TaxTotal,
    decimal? Total,
    IReadOnlyList<DocumentLine> Lines,
    IReadOnlyList<ValidationIssue> Issues)
{
    public DocumentAdjustment? HeaderDiscount { get; init; }
    public IReadOnlyList<DocumentTaxBreakdown> VatBreakdown { get; init; } = [];
    public IReadOnlyList<DocumentTaxBreakdown> Surcharges { get; init; } = [];
    public IReadOnlyList<DocumentTaxBreakdown> Withholdings { get; init; } = [];

    public bool RequiresReview =>
        DocumentType == DocumentType.Unknown ||
        Issues.Any(issue => issue.Severity >= ValidationSeverity.Warning);
}
