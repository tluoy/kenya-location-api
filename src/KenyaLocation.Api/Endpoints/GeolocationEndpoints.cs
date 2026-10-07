using KenyaLocation.Api.Database;
using Npgsql;
using KenyaLocation.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace KenyaLocation.Api.Endpoints;

public static class GeolocationEndpoints
{
    public static void MapGeolocationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/geolocation/reverse", async (
            [Description("Latitude in decimal degrees. Valid range: -90 to 90.")]
            [FromQuery(Name = "latitude")] double latitude,

            [Description("Longitude in decimal degrees. Valid range: -180 to 180.")]
            [FromQuery(Name = "longitude")] double longitude,
            NpgsqlDataSource db) =>
        {
            if (latitude is < -90 or > 90)
                return ApiResults.BadRequest(
                    "Latitude must be between -90 and 90.",
                    "INVALID_LATITUDE");

            if (longitude is < -180 or > 180)
                return ApiResults.BadRequest(
                    "Longitude must be between -180 and 180.",
                    "INVALID_LONGITUDE"
                );

            const string sql = """
                WITH point AS (
                    SELECT ST_SetSRID(
                        ST_MakePoint(@longitude, @latitude),
                        4326
                    ) AS geom
                ),
                admin_matches AS (
                    SELECT
                        a.id,
                        a.code,
                        a.name,
                        a.level,
                        a.level_name,
                        a.parent_id,
                        ST_Distance(
                            a.geometry::geography,
                            point.geom::geography
                        ) AS distance_meters,
                        ST_Y(a.centroid::geometry) AS latitude,
                        ST_X(a.centroid::geometry) AS longitude
                    FROM admin_units a
                    CROSS JOIN point
                    WHERE ST_Covers(a.geometry, point.geom)
                ),
                place_matches AS (
                    SELECT
                        p.id,
                        p.name,
                        p.place_type,
                        p.admin_unit_id,
                        ST_Y(p.centroid::geometry) AS latitude,
                        ST_X(p.centroid::geometry) AS longitude
                    FROM places p
                    CROSS JOIN point
                    WHERE ST_Covers(p.geometry, point.geom)
                )
                SELECT
                    'admin' AS result_type,
                    a.id,
                    a.code,
                    a.name,
                    a.level,
                    a.level_name,
                    a.parent_id,
                    a.distance_meters,
                    a.latitude,
                    a.longitude,
                    NULL::text AS place_type,
                    NULL::text AS admin_unit_id
                FROM admin_matches a

                UNION ALL

                SELECT
                    'place' AS result_type,
                    p.id,
                    NULL::text AS code,
                    p.name,
                    NULL::integer AS level,
                    NULL::text AS level_name,
                    NULL::text AS parent_id,
                    0::double precision AS distance_meters,
                    p.latitude,
                    p.longitude,
                    p.place_type,
                    p.admin_unit_id
                FROM place_matches p

                ORDER BY
                    result_type,
                    level NULLS LAST
                """;

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                new NpgsqlParameter<double>("latitude", latitude),
                new NpgsqlParameter<double>("longitude", longitude)
            );

            var administrative = rows
                .Where(x => string.Equals(
                    x["result_type"]?.ToString(),
                    "admin",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var places = rows
                .Where(x => string.Equals(
                    x["result_type"]?.ToString(),
                    "place",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            ReverseGeolocationAdminUnitResponse? AdminUnit(
                string levelName)
            {
                var row = administrative.FirstOrDefault(x =>
                    string.Equals(
                        x["level_name"]?.ToString(),
                        levelName,
                        StringComparison.OrdinalIgnoreCase));

                if (row is null)
                    return null;

                return new ReverseGeolocationAdminUnitResponse(
                    Id: (string)row["id"]!,
                    Code: (string)row["code"]!,
                    Name: (string)row["name"]!);
            }

            var response = new ReverseGeolocationResponse(
                Latitude: latitude,
                Longitude: longitude,
                Administrative:
                    new ReverseGeolocationAdministrativeResponse(
                        County: AdminUnit("county"),
                        SubCounty: AdminUnit("sub-county"),
                        Ward: AdminUnit("ward")),
                Places:
                    places
                        .Select(p => new ReverseGeolocationPlaceResponse(
                            Id: (string)p["id"]!,
                            Name: (string)p["name"]!,
                            Type: (string)p["place_type"]!))
                        .ToList());

            return Results.Ok(response);
        })
        .WithTags("Geolocation")
        .WithName("ReverseGeolocation")
        .WithSummary("Reverse geocode coordinates")
        .WithDescription("Returns the Kenya county, sub-county, ward, and places containing the supplied coordinates.")
        .Produces<ReverseGeolocationResponse>(StatusCodes.Status200OK)
        .Produces<ApiError>(StatusCodes.Status400BadRequest);
    }
}