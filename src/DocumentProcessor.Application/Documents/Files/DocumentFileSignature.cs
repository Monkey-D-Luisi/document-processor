namespace DocumentProcessor.Application.Documents.Files;

public static class DocumentFileSignature
{
    public static bool Matches(string mediaType, ReadOnlySpan<byte> content)
    {
        mediaType = SupportedDocumentMediaTypes.Normalize(mediaType);

        return mediaType switch
        {
            "application/pdf" => StartsWith(content, "%PDF-"u8),
            "image/jpeg" => content.Length >= 3 &&
                            content[0] == 0xFF &&
                            content[1] == 0xD8 &&
                            content[2] == 0xFF,
            "image/png" => content.Length >= 8 &&
                           content[..8].SequenceEqual(
                               new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => content.Length >= 12 &&
                            content[..4].SequenceEqual("RIFF"u8) &&
                            content.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
}
