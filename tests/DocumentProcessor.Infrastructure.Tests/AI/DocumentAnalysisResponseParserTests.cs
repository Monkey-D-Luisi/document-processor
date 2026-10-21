using DocumentProcessor.Infrastructure.AI;

namespace DocumentProcessor.Infrastructure.Tests.AI;

public sealed class DocumentAnalysisResponseParserTests
{
    private const string ValidInvoiceJson = """
        {
          "documentType": "invoice",
          "supplierName": "Example SL",
          "supplierTaxId": "B12345678",
          "customerName": null,
          "customerTaxId": null,
          "documentNumber": "F-2026-001",
          "issueDate": "2026-08-25",
          "dueDate": null,
          "currency": "EUR",
          "subtotal": 100,
          "taxTotal": 21,
          "total": 121,
          "headerDiscount": null,
          "lines": [
            {
              "description": "Development services",
              "quantity": 1,
              "unitPrice": 100,
              "taxRate": 21,
              "lineTotal": 100,
              "discount": null,
              "taxAmount": 21
            }
          ],
          "vatBreakdown": [{ "base": 100, "rate": 21, "amount": 21 }],
          "surcharges": [],
          "withholdings": [],
          "uncertainFields": [],
          "warnings": []
        }
        """;

    [Fact]
    public void Parses_structured_invoice()
    {
        var result = DocumentAnalysisResponseParser.Parse(ValidInvoiceJson);

        Assert.Equal("invoice", result.DocumentType);
        Assert.Equal(121m, result.Total);
        Assert.Single(result.Lines);
        Assert.Equal(21m, result.Lines[0]!.TaxAmount);
        Assert.Single(result.VatBreakdown);
    }

    [Fact]
    public void Rejects_unknown_properties()
    {
        var json = ValidInvoiceJson.Replace(
            "\"subtotal\": 100,",
            "\"subtotal\": 100,\n  \"unexpected\": true,");

        var act = () => DocumentAnalysisResponseParser.Parse(json);

        Assert.ThrowsAny<DocumentProcessor.Application.Common.Exceptions.DocumentAnalysisException>(act);
    }

    [Fact]
    public void Rejects_missing_required_properties()
    {
        var json = ValidInvoiceJson.Replace("\"taxAmount\": 21", string.Empty);

        var act = () => DocumentAnalysisResponseParser.Parse(json);

        Assert.ThrowsAny<DocumentProcessor.Application.Common.Exceptions.DocumentAnalysisException>(act);
    }

    [Fact]
    public void Rejects_null_line_items()
    {
        var json = ValidInvoiceJson.Replace("\"lines\": [", "\"lines\": [null,");

        var act = () => DocumentAnalysisResponseParser.Parse(json);

        Assert.ThrowsAny<DocumentProcessor.Application.Common.Exceptions.DocumentAnalysisException>(act);
    }
}
