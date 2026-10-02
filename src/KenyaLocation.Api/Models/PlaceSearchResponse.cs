namespace KenyaLocation.Api.Models;

public sealed record PlaceSearchResponse(
    string Id,
    string Name,
    string PlaceType,
    double Latitude,
    double Longitude,
    string? Ward,
    string? SubCounty,
    string? County,
    string? AdminUnitId
);