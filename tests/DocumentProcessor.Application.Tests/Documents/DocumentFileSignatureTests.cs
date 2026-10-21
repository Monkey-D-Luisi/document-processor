using DocumentProcessor.Application.Common.Files;

namespace DocumentProcessor.Application.Tests.Documents;

public sealed class DocumentFileSignatureTests
{
    [Fact]
    public void Pdf_signature_is_accepted()
    {
        var content = "%PDF-1.7 fake"u8.ToArray();

        Assert.True(DocumentFileSignature.Matches("application/pdf", content));
    }

    [Fact]
    public void Spoofed_pdf_is_rejected()
    {
        var content = "this is not a pdf"u8.ToArray();

        Assert.False(DocumentFileSignature.Matches("application/pdf", content));
    }
}
