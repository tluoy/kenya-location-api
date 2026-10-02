namespace KenyaLocation.Api.Models;

public static class ApiResults
{
    public static IResult BadRequest(string error, string? code = null)
    {
        return Results.BadRequest(
            new ApiError(error, code));
    }

    public static IResult NotFound(string error, string id)
    {
        return Results.NotFound(
            new ApiNotFoundError(error, id));
    }
}