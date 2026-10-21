using DocumentProcessor.Application.Common.Files;
using FluentAssertions;

namespace DocumentProcessor.Application.Tests.Documents;

public sealed class DocumentFileSignatureTests
{
    [Fact]
    public void Pdf_signature_is_accepted()
    {
        var content = "%PDF-1.7 fake"u8.ToArray();

        DocumentFileSignature.Matches("application/pdf", content)
            .Should().BeTrue();
    }

    [Fact]
    public void Spoofed_pdf_is_rejected()
    {
        var content = "this is not a pdf"u8.ToArray();

        DocumentFileSignature.Matches("application/pdf", content)
            .Should().BeFalse();
    }
}
