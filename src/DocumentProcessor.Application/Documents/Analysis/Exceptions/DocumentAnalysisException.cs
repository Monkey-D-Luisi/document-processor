using DocumentProcessor.Application.Documents.Analysis.Models;

namespace DocumentProcessor.Application.Documents.Analysis.Exceptions;

public sealed class DocumentAnalysisException : Exception
{
    public DocumentAnalysisException(string message)
        : this(message, DocumentAnalysisFailureKind.InvalidOutput)
    {
    }

    public DocumentAnalysisException(string message, Exception innerException)
        : this(message, DocumentAnalysisFailureKind.Unknown, null, innerException)
    {
    }

    public DocumentAnalysisException(
        string message,
        DocumentAnalysisFailureKind failureKind,
        ProviderProcessingMetrics? metrics = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        Metrics = metrics;
    }

    public DocumentAnalysisFailureKind FailureKind { get; }
    public ProviderProcessingMetrics? Metrics { get; }
}

public enum DocumentAnalysisFailureKind
{
    InvalidOutput,
    Refused,
    Incomplete,
    Authentication,
    RateLimited,
    RequestRejected,
    ProviderUnavailable,
    Timeout,
    Unknown,
}
