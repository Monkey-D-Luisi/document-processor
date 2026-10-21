namespace DocumentProcessor.Domain.Documents;

public sealed record DocumentTaxBreakdown(decimal? Base, decimal? Rate, decimal? Amount);
