using DocumentProcessor.Application.Common.Models;
using DocumentProcessor.Application.Documents.Models;
using DocumentProcessor.Domain.Documents.Repositories;
using MediatR;

namespace DocumentProcessor.Application.Documents.Queries.GetDocument;

public sealed class GetDocumentQueryHandler(IDocumentRepository repository)
    : IRequestHandler<GetDocumentQuery, Result<DocumentResponse>>
{
    public async Task<Result<DocumentResponse>> Handle(
        GetDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.Id, cancellationToken);

        return document is null
            ? Result<DocumentResponse>.Failure(
                Error.NotFound("document.not_found", "Document not found."))
            : Result<DocumentResponse>.Success(DocumentResponse.FromDomain(document));
    }
}
