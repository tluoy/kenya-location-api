namespace KenyaLocation.Api.Models;

public sealed record LocationParentResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string LevelName);

public sealed record LocationDetailResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string LevelName,
    LocationParentResponse? Parent,
    double Latitude,
    double Longitude,
    string Source,
    string SourceVersion);