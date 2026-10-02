namespace KenyaLocation.Api.Models;

public sealed record ReverseGeolocationAdminUnitResponse(
    string Id,
    string Code,
    string Name);

public sealed record ReverseGeolocationPlaceResponse(
    string Id,
    string Name,
    string Type);

public sealed record ReverseGeolocationAdministrativeResponse(
    ReverseGeolocationAdminUnitResponse? County,
    ReverseGeolocationAdminUnitResponse? SubCounty,
    ReverseGeolocationAdminUnitResponse? Ward);

public sealed record ReverseGeolocationResponse(
    double Latitude,
    double Longitude,
    ReverseGeolocationAdministrativeResponse Administrative,
    List<ReverseGeolocationPlaceResponse> Places);