using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Application.Documents.Models;
using MediatR;

namespace DocumentProcessor.Application.Documents.Commands.ProcessDocument;

public sealed record ProcessDocumentCommand(
    string FileName,
    string MediaType,
    byte[] Content)
    : IRequest<Result<DocumentResponse>>;
