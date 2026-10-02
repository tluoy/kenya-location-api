namespace KenyaLocation.Api.Models;

public sealed record NearbyLocationResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string LevelName,
    double DistanceMeters,
    double Latitude,
    double Longitude
);