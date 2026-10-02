namespace KenyaLocation.Api.Models;

public sealed record CountyResponse(
    string Id,
    string Code,
    string Name,
    double Latitude,
    double Longitude
);