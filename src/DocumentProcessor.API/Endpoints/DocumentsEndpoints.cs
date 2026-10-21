using DocumentProcessor.API.Mapping;
using DocumentProcessor.Application.Common.Files;
using DocumentProcessor.Application.Documents.Commands.ProcessDocument;
using DocumentProcessor.Application.Documents.Models;
using DocumentProcessor.Application.Documents.Queries.GetDocument;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessor.API.Endpoints;

internal static class DocumentsEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/documents")
            .WithTags("Documents");

        group.MapGet("/antiforgery", GetAntiforgeryTokenAsync)
            .Produces<AntiforgeryTokenResponse>();

        group.MapPost("/process", ProcessAsync)
            .WithMetadata(new RequestSizeLimitAttribute(
                SupportedDocumentMediaTypes.MaxFileSizeBytes + 64 * 1024))
            .Produces<DocumentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<DocumentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static IResult GetAntiforgeryTokenAsync(
        IAntiforgery antiforgery,
        HttpContext context) =>
        Results.Ok(new AntiforgeryTokenResponse(
            antiforgery.GetAndStoreTokens(context).RequestToken!));

    private static async Task<IResult> ProcessAsync(
        IFormFile file,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (file.Length > SupportedDocumentMediaTypes.MaxFileSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "document.file_too_large",
                detail: "Maximum document size is 20 MB.");
        }

        var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        var mediaType = string.IsNullOrWhiteSpace(file.ContentType)
            ? InferMediaType(safeFileName)
            : file.ContentType;
        mediaType = SupportedDocumentMediaTypes.Normalize(mediaType);

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream(
            file.Length > int.MaxValue ? 0 : (int)file.Length);

        await stream.CopyToAsync(memory, cancellationToken);

        var result = await sender.Send(
            new ProcessDocumentCommand(
                safeFileName,
                mediaType,
                memory.ToArray()),
            cancellationToken);

        if (result.IsFailure)
            return result.ToHttpResult();

        var response = result.Value!;
        return Results.Created($"/api/v1/documents/{response.Id}", response);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDocumentQuery(id), cancellationToken);
        return result.ToHttpResult();
    }

    private static string InferMediaType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };

    private sealed record AntiforgeryTokenResponse(string RequestToken);
}
