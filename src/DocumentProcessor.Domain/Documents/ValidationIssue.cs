namespace DocumentProcessor.Domain.Documents;

public enum ValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public sealed record ValidationIssue(
    string Code,
    string Message,
    ValidationSeverity Severity)
{
    public string? Field { get; init; }
    public int? LineIndex { get; init; }
}
