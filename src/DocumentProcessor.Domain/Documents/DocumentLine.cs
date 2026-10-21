namespace DocumentProcessor.Domain.Documents;

public sealed record DocumentLine(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TaxRate,
    decimal? LineTotal,
    DocumentAdjustment? Discount = null,
    decimal? TaxAmount = null);
