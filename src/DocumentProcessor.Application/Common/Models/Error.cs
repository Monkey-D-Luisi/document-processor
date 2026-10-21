namespace DocumentProcessor.Application.Common.Models;

public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    ExternalDependency = 3,
    Unexpected = 4,
}

public sealed record Error(string Code, string Message, ErrorType Type, Guid? ResourceId = null)
{
    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error External(string code, string message, Guid? resourceId = null) =>
        new(code, message, ErrorType.ExternalDependency, resourceId);
}
