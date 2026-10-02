namespace KenyaLocation.Api.Models;

public sealed record LocationSearchResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string LevelName,
    double Latitude,
    double Longitude,
    string? ParentId
);