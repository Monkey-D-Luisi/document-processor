using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Application.Documents.Models;
using MediatR;

namespace DocumentProcessor.Application.Documents.Queries.GetDocument;

public sealed record GetDocumentQuery(Guid Id)
    : IRequest<Result<DocumentResponse>>;
