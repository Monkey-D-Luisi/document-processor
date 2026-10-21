using DocumentProcessor.Application.Common.Models;

namespace DocumentProcessor.API.Mapping;

internal static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        var error = result.Error
            ?? new Error("unexpected", "Unexpected failure.", ErrorType.Unexpected);

        return error.Type switch
        {
            ErrorType.Validation => Results.BadRequest(new
            {
                error.Code,
                error.Message,
            }),
            ErrorType.NotFound => Results.NotFound(new
            {
                error.Code,
                error.Message,
            }),
            ErrorType.Conflict => Results.Conflict(new
            {
                error.Code,
                error.Message,
            }),
            ErrorType.ExternalDependency => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: error.Code,
                detail: error.Message,
                extensions: error.ResourceId is null
                    ? null
                    : new Dictionary<string, object?> { ["documentId"] = error.ResourceId }),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code,
                detail: error.Message),
        };
    }
}
