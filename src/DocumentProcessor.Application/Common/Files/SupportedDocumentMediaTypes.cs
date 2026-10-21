namespace DocumentProcessor.Application.Common.Files;

public static class SupportedDocumentMediaTypes
{
    public const int MaxFileSizeBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> Supported =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    public static bool Contains(string? mediaType) =>
        Supported.Contains(Normalize(mediaType));

    public static string Normalize(string? mediaType) =>
        string.IsNullOrWhiteSpace(mediaType)
            ? string.Empty
            : mediaType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
