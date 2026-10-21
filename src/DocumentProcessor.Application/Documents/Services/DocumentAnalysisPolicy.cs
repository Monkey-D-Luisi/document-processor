using System.Globalization;
using DocumentProcessor.Application.Common.Exceptions;
using DocumentProcessor.Application.Common.Interfaces;
using DocumentProcessor.Domain.Documents;

namespace DocumentProcessor.Application.Documents.Services;

public static class DocumentAnalysisPolicy
{
    private const int MaximumLines = 100;
    private const int MaximumSignals = 50;

    public static DocumentAnalysis Build(DocumentAnalysisCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ValidateContract(candidate);

        var issues = new List<ValidationIssue>();
        var documentType = ParseDocumentType(candidate.DocumentType);
        var issueDate = ParseDate(candidate.IssueDate, "issueDate", issues);
        var dueDate = ParseDate(candidate.DueDate, "dueDate", issues);
        var currency = NormalizeCurrency(candidate.Currency, issues);
        var minorUnits = currency is not null &&
                         CurrencyMinorUnitCatalog.TryGetMinorUnit(currency, out var scale)
            ? scale
            : (int?)null;

        if (issueDate is not null && dueDate is not null && dueDate < issueDate)
            AddIssue(issues, "due_date_before_issue_date", "Due date precedes issue date.", "dueDate");

        AddModelSignals(candidate, issues);
        AddRequiredFieldChecks(candidate, documentType, issues);

        var lines = candidate.Lines
            .Select((line, index) => ToDomainLine(line!, index + 1, issues))
            .ToArray();
        var headerDiscount = ToDomainAdjustment(candidate.HeaderDiscount);
        var vatBreakdown = ToDomainBreakdown(candidate.VatBreakdown);
        var surcharges = ToDomainBreakdown(candidate.Surcharges);
        var withholdings = ToDomainBreakdown(candidate.Withholdings);

        AddFinancialChecks(candidate, documentType, currency, minorUnits, lines, issues);

        return new DocumentAnalysis(
            documentType,
            Clean(candidate.SupplierName),
            Clean(candidate.SupplierTaxId),
            Clean(candidate.CustomerName),
            Clean(candidate.CustomerTaxId),
            Clean(candidate.DocumentNumber),
            issueDate,
            dueDate,
            currency,
            candidate.Subtotal,
            candidate.TaxTotal,
            candidate.Total,
            lines,
            issues)
        {
            HeaderDiscount = headerDiscount,
            VatBreakdown = vatBreakdown,
            Surcharges = surcharges,
            Withholdings = withholdings,
        };
    }

    private static void ValidateContract(DocumentAnalysisCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.DocumentType) || candidate.DocumentType.Length > 32 ||
            Exceeds(candidate.SupplierName, 300) || Exceeds(candidate.SupplierTaxId, 50) ||
            Exceeds(candidate.CustomerName, 300) || Exceeds(candidate.CustomerTaxId, 50) ||
            Exceeds(candidate.DocumentNumber, 100) || Exceeds(candidate.IssueDate, 10) ||
            Exceeds(candidate.DueDate, 10) || Exceeds(candidate.Currency, 3) ||
            candidate.Lines is null || candidate.Lines.Count > MaximumLines ||
            candidate.UncertainFields is null || candidate.UncertainFields.Count > MaximumSignals ||
            candidate.Warnings is null || candidate.Warnings.Count > MaximumSignals ||
            candidate.VatBreakdown is null || candidate.VatBreakdown.Count > MaximumSignals ||
            candidate.Surcharges is null || candidate.Surcharges.Count > MaximumSignals ||
            candidate.Withholdings is null || candidate.Withholdings.Count > MaximumSignals)
        {
            throw new DocumentAnalysisException("The model returned data outside the extraction contract.");
        }

        if (candidate.Lines.Any(line => line is null || Exceeds(line.Description, 500)) ||
            candidate.UncertainFields.Any(value => value is null || value.Length > 100) ||
            candidate.Warnings.Any(value => value is null || value.Length > 500) ||
            candidate.VatBreakdown.Any(item => item is null) ||
            candidate.Surcharges.Any(item => item is null) ||
            candidate.Withholdings.Any(item => item is null))
        {
            throw new DocumentAnalysisException("The model returned data outside the extraction contract.");
        }
    }

    private static DocumentType ParseDocumentType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "invoice" => DocumentType.Invoice,
            "delivery_note" => DocumentType.DeliveryNote,
            "quote" => DocumentType.Quote,
            _ => DocumentType.Unknown,
        };

    private static DateOnly? ParseDate(
        string? value,
        string field,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
            return parsed;

        AddIssue(issues, "invalid_date", $"{field} is not a valid ISO date.", field);
        return null;
    }

    private static string? NormalizeCurrency(
        string? currency,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return null;

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(value => value is < 'A' or > 'Z'))
        {
            AddIssue(issues, "invalid_currency", "Currency must be a three-letter ISO 4217 code.", "currency");
        }
        else if (!CurrencyMinorUnitCatalog.TryGetMinorUnit(normalized, out _))
        {
            AddIssue(issues, "unsupported_currency", "Currency is not present in the supported ISO 4217 snapshot.", "currency");
        }

        return normalized;
    }

    private static void AddModelSignals(
        DocumentAnalysisCandidate candidate,
        ICollection<ValidationIssue> issues)
    {
        foreach (var field in candidate.UncertainFields
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddIssue(issues, "uncertain_field", $"The model marked '{field.Trim()}' as uncertain.", field.Trim());
        }

        foreach (var warning in candidate.Warnings
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddIssue(issues, "model_warning", warning.Trim());
        }
    }

    private static void AddRequiredFieldChecks(
        DocumentAnalysisCandidate candidate,
        DocumentType documentType,
        ICollection<ValidationIssue> issues)
    {
        if (documentType == DocumentType.Unknown)
        {
            AddIssue(
                issues,
                "unknown_document_type",
                "The document could not be classified as invoice, delivery note, or quote.",
                "documentType");
            return;
        }

        Require(candidate.SupplierName, "supplierName", issues);
        Require(candidate.DocumentNumber, "documentNumber", issues);
        Require(candidate.IssueDate, "issueDate", issues);

        if (documentType is not (DocumentType.Invoice or DocumentType.Quote))
            return;

        Require(candidate.Currency, "currency", issues);
        Require(candidate.Subtotal, "subtotal", issues);
        Require(candidate.TaxTotal, "taxTotal", issues);
        Require(candidate.Total, "total", issues);

        if (candidate.Lines.Count == 0)
            AddIssue(issues, "missing_lines", "No line items were extracted for this financial document.", "lines");
    }

    private static void AddFinancialChecks(
        DocumentAnalysisCandidate candidate,
        DocumentType documentType,
        string? currency,
        int? minorUnits,
        IReadOnlyList<DocumentLine> lines,
        ICollection<ValidationIssue> issues)
    {
        if (!HasFinancialData(candidate, lines))
            return;

        if (minorUnits is null)
        {
            if (currency is null && documentType is not (DocumentType.Invoice or DocumentType.Quote))
                AddIssue(issues, "missing_currency", "Currency is required to validate monetary amounts.", "currency");

            return;
        }

        var tolerance = CurrencyMinorUnitCatalog.GetTolerance(minorUnits.Value);
        AddPrecisionChecks(candidate, lines, minorUnits.Value, issues);
        AddNegativeAmountChecks(candidate, lines, issues);

        try
        {
            ValidateLineAmounts(candidate, lines, minorUnits.Value, tolerance, issues);
            ValidateBreakdown(candidate.VatBreakdown, "vatBreakdown", minorUnits.Value, tolerance, issues);
            ValidateBreakdown(candidate.Surcharges, "surcharges", minorUnits.Value, tolerance, issues);
            ValidateBreakdown(candidate.Withholdings, "withholdings", minorUnits.Value, tolerance, issues);
            ValidateTaxTotal(candidate, lines, minorUnits.Value, tolerance, issues);
            ValidatePayableTotal(candidate, minorUnits.Value, tolerance, issues);
        }
        catch (OverflowException)
        {
            AddIssue(issues, "amount_out_of_range", "A monetary calculation exceeded the supported decimal range.");
        }
    }

    private static void ValidateLineAmounts(
        DocumentAnalysisCandidate candidate,
        IReadOnlyList<DocumentLine> lines,
        int minorUnits,
        decimal tolerance,
        ICollection<ValidationIssue> issues)
    {
        var isFinancialDocument = candidate.DocumentType.Equals("invoice", StringComparison.OrdinalIgnoreCase) ||
                                  candidate.DocumentType.Equals("quote", StringComparison.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = candidate.Lines[index]!;
            var lineNumber = index + 1;
            var baseAmount = line.Quantity is not null && line.UnitPrice is not null
                ? line.Quantity.Value * line.UnitPrice.Value
                : (decimal?)null;

            ValidateAdjustment(line.Discount, $"lines[{lineNumber}].discount", lineNumber, baseAmount,
                minorUnits, tolerance, issues);

            if (line.Quantity is < 0)
                AddIssue(issues, "negative_quantity", "Line quantity is negative.", "quantity", lineNumber);

            if (line.TaxRate is < 0 or > 100)
                AddIssue(issues, "invalid_tax_rate", "Line tax rate is outside the expected 0 to 100 percent range.", "taxRate", lineNumber);

            if (line.Quantity is null || line.UnitPrice is null || line.LineTotal is null)
            {
                if (isFinancialDocument)
                    AddIssue(issues, "incomplete_line_amount", "Line quantity, unit price, or net amount is missing.", "lineTotal", lineNumber);

                continue;
            }

            if (line.Discount is { Amount: null })
                continue;

            var expectedLineTotal = CurrencyMinorUnitCatalog.Round(
                baseAmount!.Value - (line.Discount?.Amount ?? 0m),
                minorUnits);
            if (Math.Abs(expectedLineTotal - line.LineTotal.Value) > tolerance)
            {
                AddIssue(
                    issues,
                    "line_total_mismatch",
                    $"Quantity × unit price minus discount does not match net line amount ({Format(expectedLineTotal)} vs {Format(line.LineTotal.Value)}).",
                    "lineTotal",
                    lineNumber);
            }

            if (line.TaxRate is not null && line.TaxAmount is null && isFinancialDocument)
                AddIssue(issues, "missing_line_tax_amount", "Line tax amount is missing.", "taxAmount", lineNumber);
            else if (line.TaxRate is not null && line.TaxAmount is not null && candidate.HeaderDiscount is null)
            {
                var expectedTax = CurrencyMinorUnitCatalog.Round(
                    line.LineTotal.Value * line.TaxRate.Value / 100m,
                    minorUnits);
                if (Math.Abs(expectedTax - line.TaxAmount.Value) > tolerance)
                    AddIssue(issues, "line_tax_mismatch", "Line tax amount does not match its rate and net base.", "taxAmount", lineNumber);
            }
            else if (line.TaxAmount is not null)
            {
                AddIssue(issues, "missing_line_tax_rate", "Line tax rate is missing.", "taxRate", lineNumber);
            }
        }

        if (lines.Count == 0 || lines.Any(line => line.LineTotal is null))
        {
            ValidateAdjustment(candidate.HeaderDiscount, "headerDiscount", null, null,
                minorUnits, tolerance, issues);
            return;
        }

        var lineSum = lines.Sum(line => line.LineTotal!.Value);
        ValidateAdjustment(candidate.HeaderDiscount, "headerDiscount", null, lineSum,
            minorUnits, tolerance, issues);

        if (candidate.Subtotal is null || candidate.HeaderDiscount is { Amount: null })
            return;

        var expectedSubtotal = CurrencyMinorUnitCatalog.Round(
            lineSum - (candidate.HeaderDiscount?.Amount ?? 0m),
            minorUnits);
        if (Math.Abs(expectedSubtotal - candidate.Subtotal.Value) > tolerance)
        {
            AddIssue(
                issues,
                "subtotal_mismatch",
                $"Net line amounts minus header discount do not match subtotal ({Format(expectedSubtotal)} vs {Format(candidate.Subtotal.Value)}).",
                "subtotal");
        }
    }

    private static void ValidateAdjustment(
        DocumentAdjustmentCandidate? adjustment,
        string field,
        int? lineIndex,
        decimal? baseAmount,
        int minorUnits,
        decimal tolerance,
        ICollection<ValidationIssue> issues)
    {
        if (adjustment is null)
            return;

        if (adjustment.Amount is null)
            AddIssue(issues, "incomplete_discount", "Discount amount is missing.", $"{field}.amount", lineIndex);

        if (adjustment.Rate is null)
            AddIssue(issues, "discount_rate_unavailable", "Discount rate could not be verified.", $"{field}.rate", lineIndex);
        else if (adjustment.Amount is not null && baseAmount is null)
            AddIssue(issues, "discount_base_unavailable", "Discount could not be checked because its base is incomplete.", field, lineIndex);
        else if (adjustment.Amount is not null)
        {
            var expected = CurrencyMinorUnitCatalog.Round(baseAmount!.Value * adjustment.Rate.Value / 100m, minorUnits);
            if (Math.Abs(expected - adjustment.Amount.Value) > tolerance)
                AddIssue(issues, "discount_mismatch", "Discount amount does not match its rate and base.", field, lineIndex);
        }

        if (adjustment.Amount is < 0 || adjustment.Rate is < 0 or > 100)
            AddIssue(issues, "invalid_discount", "Discount amount or rate is outside the expected range.", field, lineIndex);
    }

    private static void ValidateBreakdown(
        IReadOnlyList<DocumentTaxBreakdownCandidate?> entries,
        string field,
        int minorUnits,
        decimal tolerance,
        ICollection<ValidationIssue> issues)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var item = entries[index]!;
            if (item.Base is null || item.Rate is null || item.Amount is null)
            {
                AddIssue(issues, "incomplete_tax_breakdown", "Tax base, rate, or amount is missing.", field, index + 1);
                continue;
            }

            if (item.Base < 0 || item.Rate < 0 || item.Rate > 100 || item.Amount < 0)
                AddIssue(issues, "invalid_tax_breakdown", "Tax breakdown contains a negative amount or invalid rate.", field, index + 1);

            var expected = CurrencyMinorUnitCatalog.Round(item.Base.Value * item.Rate.Value / 100m, minorUnits);
            if (Math.Abs(expected - item.Amount.Value) > tolerance)
                AddIssue(issues, "tax_breakdown_mismatch", "Tax amount does not match its rate and base.", field, index + 1);
        }
    }

    private static void ValidateTaxTotal(
        DocumentAnalysisCandidate candidate,
        IReadOnlyList<DocumentLine> lines,
        int minorUnits,
        decimal tolerance,
        ICollection<ValidationIssue> issues)
    {
        if (candidate.VatBreakdown.Count == 0 && candidate.TaxTotal is > 0)
            AddIssue(issues, "missing_vat_breakdown", "VAT breakdown is required to verify a non-zero VAT total.", "vatBreakdown");

        if (candidate.TaxTotal is null)
            return;

        if (candidate.VatBreakdown.Count > 0 && candidate.VatBreakdown.All(item => item!.Amount is not null))
        {
            var expected = CurrencyMinorUnitCatalog.Round(
                candidate.VatBreakdown.Sum(item => item!.Amount!.Value),
                minorUnits);
            if (Math.Abs(expected - candidate.TaxTotal.Value) > tolerance)
                AddIssue(issues, "vat_total_mismatch", "VAT breakdown amounts do not match tax total.", "taxTotal");
        }

        if (candidate.Subtotal is not null && candidate.VatBreakdown.Count > 0 &&
            candidate.VatBreakdown.All(item => item!.Base is not null))
        {
            var vatBases = CurrencyMinorUnitCatalog.Round(
                candidate.VatBreakdown.Sum(item => item!.Base!.Value),
                minorUnits);
            if (Math.Abs(vatBases - candidate.Subtotal.Value) > tolerance)
                AddIssue(issues, "vat_base_mismatch", "VAT breakdown bases do not match subtotal.", "vatBreakdown");
        }

        if (lines.Count > 0 && lines.All(line => line.TaxAmount is not null))
        {
            var lineTaxes = CurrencyMinorUnitCatalog.Round(lines.Sum(line => line.TaxAmount!.Value), minorUnits);
            if (Math.Abs(lineTaxes - candidate.TaxTotal.Value) > tolerance)
                AddIssue(issues, "line_tax_sum_mismatch", "Line tax amounts do not match tax total.", "taxTotal");
        }
    }

    private static void ValidatePayableTotal(
        DocumentAnalysisCandidate candidate,
        int minorUnits,
        decimal tolerance,
        ICollection<ValidationIssue> issues)
    {
        if (candidate.Subtotal is null || candidate.TaxTotal is null || candidate.Total is null ||
            candidate.Surcharges.Any(item => item!.Amount is null) ||
            candidate.Withholdings.Any(item => item!.Amount is null))
            return;

        var surchargeTotal = candidate.Surcharges.Sum(item => item!.Amount!.Value);
        var withholdingTotal = candidate.Withholdings.Sum(item => item!.Amount!.Value);
        var expected = CurrencyMinorUnitCatalog.Round(
            candidate.Subtotal.Value + candidate.TaxTotal.Value + surchargeTotal - withholdingTotal,
            minorUnits);

        if (Math.Abs(expected - candidate.Total.Value) > tolerance)
        {
            AddIssue(
                issues,
                "total_mismatch",
                $"Subtotal + VAT + surcharges − withholdings do not match total ({Format(expected)} vs {Format(candidate.Total.Value)}).",
                "total");
        }
    }

    private static void AddPrecisionChecks(
        DocumentAnalysisCandidate candidate,
        IReadOnlyList<DocumentLine> lines,
        int minorUnits,
        ICollection<ValidationIssue> issues)
    {
        CheckPrecision(candidate.Subtotal, minorUnits, "subtotal", issues);
        CheckPrecision(candidate.TaxTotal, minorUnits, "taxTotal", issues);
        CheckPrecision(candidate.Total, minorUnits, "total", issues);
        CheckPrecision(candidate.HeaderDiscount?.Amount, minorUnits, "headerDiscount.amount", issues);
        for (var index = 0; index < lines.Count; index++)
        {
            CheckPrecision(lines[index].LineTotal, minorUnits, "lineTotal", issues, index + 1);
            CheckPrecision(lines[index].TaxAmount, minorUnits, "taxAmount", issues, index + 1);
            CheckPrecision(lines[index].Discount?.Amount, minorUnits, "discount.amount", issues, index + 1);
        }

        CheckBreakdownPrecision(candidate.VatBreakdown, minorUnits, "vatBreakdown", issues);
        CheckBreakdownPrecision(candidate.Surcharges, minorUnits, "surcharges", issues);
        CheckBreakdownPrecision(candidate.Withholdings, minorUnits, "withholdings", issues);
    }

    private static void CheckBreakdownPrecision(
        IReadOnlyList<DocumentTaxBreakdownCandidate?> entries,
        int minorUnits,
        string field,
        ICollection<ValidationIssue> issues)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            CheckPrecision(entries[index]!.Base, minorUnits, $"{field}.base", issues, index + 1);
            CheckPrecision(entries[index]!.Amount, minorUnits, $"{field}.amount", issues, index + 1);
        }
    }

    private static void CheckPrecision(
        decimal? amount,
        int minorUnits,
        string field,
        ICollection<ValidationIssue> issues,
        int? lineIndex = null)
    {
        if (amount is not null && CurrencyMinorUnitCatalog.Round(amount.Value, minorUnits) != amount.Value)
            AddIssue(issues, "excess_currency_precision", "Amount has more fractional digits than the currency permits.", field, lineIndex);
    }

    private static void AddNegativeAmountChecks(
        DocumentAnalysisCandidate candidate,
        IReadOnlyList<DocumentLine> lines,
        ICollection<ValidationIssue> issues)
    {
        if (candidate.Total is < 0 || candidate.Subtotal is < 0 || candidate.TaxTotal is < 0)
            AddIssue(issues, "negative_amount", "One or more document totals are negative and require review.", "total");

        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].LineTotal < 0 || lines[index].TaxAmount < 0)
                AddIssue(issues, "negative_line_amount", "A line amount is negative and requires review.", "lineTotal", index + 1);
        }
    }

    private static bool HasFinancialData(
        DocumentAnalysisCandidate candidate,
        IReadOnlyList<DocumentLine> lines) =>
        candidate.Subtotal is not null || candidate.TaxTotal is not null || candidate.Total is not null ||
        candidate.HeaderDiscount is not null || candidate.VatBreakdown.Count > 0 ||
        candidate.Surcharges.Count > 0 || candidate.Withholdings.Count > 0 ||
        lines.Any(line => line.Quantity is not null || line.UnitPrice is not null ||
                          line.LineTotal is not null || line.TaxAmount is not null || line.Discount is not null);

    private static DocumentLine ToDomainLine(
        DocumentLineCandidate candidate,
        int lineIndex,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(candidate.Description))
            AddIssue(issues, "missing_line_description", "Line description is missing.", "description", lineIndex);

        return new DocumentLine(
            Clean(candidate.Description) ?? string.Empty,
            candidate.Quantity,
            candidate.UnitPrice,
            candidate.TaxRate,
            candidate.LineTotal,
            ToDomainAdjustment(candidate.Discount),
            candidate.TaxAmount);
    }

    private static DocumentAdjustment? ToDomainAdjustment(DocumentAdjustmentCandidate? adjustment) =>
        adjustment is null ? null : new DocumentAdjustment(adjustment.Rate, adjustment.Amount);

    private static IReadOnlyList<DocumentTaxBreakdown> ToDomainBreakdown(
        IReadOnlyList<DocumentTaxBreakdownCandidate?> values) =>
        values.Select(item => new DocumentTaxBreakdown(item!.Base, item.Rate, item.Amount)).ToArray();

    private static void Require(string? value, string field, ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            AddIssue(issues, "missing_required_field", $"Expected field '{field}' was not found.", field);
    }

    private static void Require(decimal? value, string field, ICollection<ValidationIssue> issues)
    {
        if (value is null)
            AddIssue(issues, "missing_required_field", $"Expected field '{field}' was not found.", field);
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string code,
        string message,
        string? field = null,
        int? lineIndex = null) =>
        issues.Add(new ValidationIssue(code, message, ValidationSeverity.Warning)
        {
            Field = field,
            LineIndex = lineIndex,
        });

    private static string Format(decimal amount) => amount.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool Exceeds(string? value, int maximum) => value is not null && value.Length > maximum;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
