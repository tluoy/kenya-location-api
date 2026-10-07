using System.Text.Json;

namespace KenyaLocation.Api.Models;

public sealed record PlaceDetailResponse(
    string Id,
    string Name,
    string PlaceType,
    double Latitude,
    double Longitude,
    JsonElement Geometry,
    string? WardId,
    string? Ward,
    string? SubCountyId,
    string? SubCounty,
    string? CountyId,
    string? County,
    string? AdminUnitId,
    string Source,
    string SourceVersion
);