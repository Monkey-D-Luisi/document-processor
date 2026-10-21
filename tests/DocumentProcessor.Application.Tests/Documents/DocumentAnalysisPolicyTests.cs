using DocumentProcessor.Application.Common.Exceptions;
using DocumentProcessor.Application.Common.Interfaces;
using DocumentProcessor.Application.Documents.Services;
using DocumentProcessor.Domain.Documents;
using FluentAssertions;

namespace DocumentProcessor.Application.Tests.Documents;

public sealed class DocumentAnalysisPolicyTests
{
    [Fact]
    public void Valid_invoice_does_not_require_review()
    {
        var analysis = DocumentAnalysisPolicy.Build(CreateCandidate());

        analysis.RequiresReview.Should().BeFalse();
        analysis.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Discounts_surcharges_withholdings_and_vat_reconcile()
    {
        var candidate = CreateCandidate(
            subtotal: 81m,
            tax: 17.01m,
            total: 98.82m,
            lines: [new DocumentLineCandidate("Service", 1m, 100m, null, 90m,
                new DocumentAdjustmentCandidate(10m, 10m))]) with
        {
            HeaderDiscount = new DocumentAdjustmentCandidate(10m, 9m),
            VatBreakdown = [new DocumentTaxBreakdownCandidate(81m, 21m, 17.01m)],
            Surcharges = [new DocumentTaxBreakdownCandidate(81m, 2m, 1.62m)],
            Withholdings = [new DocumentTaxBreakdownCandidate(81m, 1m, 0.81m)],
        };

        var analysis = DocumentAnalysisPolicy.Build(candidate);

        analysis.RequiresReview.Should().BeFalse();
        analysis.HeaderDiscount.Should().Be(new DocumentAdjustment(10m, 9m));
        analysis.Surcharges.Should().ContainSingle();
        analysis.Withholdings.Should().ContainSingle();
    }

    [Fact]
    public void Total_mismatch_requires_review()
    {
        var candidate = CreateCandidate(total: 125m);

        var analysis = DocumentAnalysisPolicy.Build(candidate);

        analysis.RequiresReview.Should().BeTrue();
        analysis.Issues.Should().Contain(issue => issue.Code == "total_mismatch");
    }

    [Fact]
    public void Line_tax_mismatch_is_associated_with_its_line()
    {
        var candidate = CreateCandidate(lines:
        [
            new DocumentLineCandidate("Service", 1m, 100m, 21m, 100m, TaxAmount: 20m),
        ]);

        var analysis = DocumentAnalysisPolicy.Build(candidate);

        analysis.Issues.Should().Contain(issue =>
            issue.Code == "line_tax_mismatch" && issue.Field == "taxAmount" && issue.LineIndex == 1);
    }

    [Theory]
    [InlineData("JPY", 100.5, 101)]
    [InlineData("KWD", 1.2344, 1.234)]
    public void Currency_precision_uses_minor_units(string currency, decimal unitPrice, decimal lineTotal)
    {
        var candidate = CreateCandidate(
            currency: currency,
            subtotal: lineTotal,
            tax: 0m,
            total: lineTotal,
            lines: [new DocumentLineCandidate("Service", 1m, unitPrice, 0m, lineTotal, TaxAmount: 0m)]) with
        {
            VatBreakdown = [],
        };

        var analysis = DocumentAnalysisPolicy.Build(candidate);

        analysis.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public void Unknown_currency_requires_human_review()
    {
        var analysis = DocumentAnalysisPolicy.Build(CreateCandidate(currency: "ABC"));

        analysis.RequiresReview.Should().BeTrue();
        analysis.Issues.Should().Contain(issue => issue.Code == "unsupported_currency");
    }

    [Fact]
    public void Missing_financial_values_and_currency_require_review()
    {
        var analysis = DocumentAnalysisPolicy.Build(CreateCandidate(
            currency: null,
            subtotal: null,
            tax: null,
            total: null,
            lines: []));

        analysis.RequiresReview.Should().BeTrue();
        analysis.Issues.Should().Contain(issue => issue.Code == "missing_required_field" && issue.Field == "currency");
        analysis.Issues.Should().Contain(issue => issue.Code == "missing_required_field" && issue.Field == "total");
    }

    [Fact]
    public void Incomplete_line_is_preserved_and_flagged()
    {
        var analysis = DocumentAnalysisPolicy.Build(CreateCandidate(
            subtotal: null,
            tax: null,
            total: null,
            lines: [new DocumentLineCandidate("", null, null, null, null)]));

        analysis.Lines.Should().ContainSingle().Which.Description.Should().BeEmpty();
        analysis.Issues.Should().Contain(issue => issue.Code == "missing_line_description" && issue.LineIndex == 1);
    }

    [Fact]
    public void Delivery_note_without_amounts_does_not_require_review()
    {
        var candidate = CreateCandidate(
            documentType: "delivery_note",
            currency: null,
            subtotal: null,
            tax: null,
            total: null,
            lines: []);

        var analysis = DocumentAnalysisPolicy.Build(candidate);

        analysis.RequiresReview.Should().BeFalse();
    }

    [Fact]
    public void More_than_one_hundred_lines_is_rejected()
    {
        var candidate = CreateCandidate(lines:
            Enumerable.Range(1, 101).Select(index => new DocumentLineCandidate($"Line {index}", null, null, null, null)).ToArray());

        var act = () => DocumentAnalysisPolicy.Build(candidate);

        act.Should().Throw<DocumentAnalysisException>();
    }

    [Fact]
    public void Currency_snapshot_contains_zero_two_and_three_fraction_digits()
    {
        CurrencyMinorUnitCatalog.TryGetMinorUnit("JPY", out var zero).Should().BeTrue();
        CurrencyMinorUnitCatalog.TryGetMinorUnit("EUR", out var two).Should().BeTrue();
        CurrencyMinorUnitCatalog.TryGetMinorUnit("KWD", out var three).Should().BeTrue();
        (zero, two, three).Should().Be((0, 2, 3));
    }

    private static DocumentAnalysisCandidate CreateCandidate(
        string documentType = "invoice",
        string? currency = "EUR",
        decimal? subtotal = 100m,
        decimal? tax = 21m,
        decimal? total = 121m,
        IReadOnlyList<DocumentLineCandidate?>? lines = null) =>
        new(
            documentType,
            "Example SL",
            "B12345678",
            "Customer SL",
            "B87654321",
            "F-001",
            "2026-08-25",
            "2026-09-25",
            currency,
            subtotal,
            tax,
            total,
            lines ?? [new DocumentLineCandidate("Service", 1m, 100m, 21m, 100m, TaxAmount: 21m)],
            [],
            [])
        {
            VatBreakdown = tax is null or 0m
                ? []
                : [new DocumentTaxBreakdownCandidate(subtotal, 21m, tax)],
        };
}
