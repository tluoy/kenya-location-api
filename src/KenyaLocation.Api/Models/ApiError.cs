namespace KenyaLocation.Api.Models;

public sealed record ApiError (
    string Error,
    string? Code = null
);