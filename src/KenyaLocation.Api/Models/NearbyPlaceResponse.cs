namespace KenyaLocation.Api.Models;

public sealed record NearbyPlaceResponse(
    string Id,
    string Name,
    string PlaceType,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    string? Ward,
    string? SubCounty,
    string? County
);