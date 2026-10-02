namespace KenyaLocation.Api.Models;

public sealed record ApiNotFoundError(
    string Error,
    string Id
);