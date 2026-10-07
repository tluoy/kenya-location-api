using Npgsql;
using KenyaLocation.Api.Models;
using KenyaLocation.Api.Database;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace KenyaLocation.Api.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/locations/counties", async (NpgsqlDataSource db) =>
        {
            const string sql = """
                SELECT
                    id,
                    code,
                    name,
                    level,
                    ST_Y(centroid::geometry) AS latitude,
                    ST_X(centroid::geometry) AS longitude
                FROM admin_units
                WHERE level = 1
                ORDER BY name
                """;

            var rows = await DbQuery.QueryRows(db, sql);

            var response = rows.Select(row =>
                new CountyResponse(
                    Id: (string)row["id"]!,
                    Code: (string)row["code"]!,
                    Name: (string)row["name"]!,
                    Latitude: Convert.ToDouble(row["latitude"]),
                    Longitude: Convert.ToDouble(row["longitude"]))
                )
                .ToList();

            return Results.Ok(response);
        })
        .WithTags("Locations")
        .WithName("GetCounties")
        .WithSummary("List all Kenya counties")
        .WithDescription("Returns the 47 counties in alphabetical order.")
        .Produces<List<CountyResponse>>(StatusCodes.Status200OK);

        app.MapGet("/api/v1/locations/search", async (
            [Description("Optional text used to search by name.")]
            [FromQuery(Name = "q")] string? q,

            [Description("Administrative level: 1 = county, 2 = sub-county, 3 = ward.")]
            [FromQuery(Name = "level")] int? level,

            [Description("Maximum number of results to return. Default: 20. Valid range: 1 to 100.")]
            [FromQuery(Name = "limit")] int? limit,
            NpgsqlDataSource db) =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 100);

            if (level is not null && level is < 1 or > 3)
            {
                return ApiResults.BadRequest(
                    "Level must be 1 (county), 2 (sub-county), or 3 (ward).",
                    "INVALID_LEVEL"
                );
            }

            var search = q?.Trim() ?? "";

            const string sql = """
                SELECT
                    id,
                    code,
                    name,
                    level,
                    level_name,
                    ST_Y(centroid::geometry) AS latitude,
                    ST_X(centroid::geometry) AS longitude,
                    parent_id
                FROM admin_units
                WHERE
                    (
                        @q = ''
                        OR normalized_name ILIKE '%' || lower(@q) || '%'
                        OR level_name ILIKE '%' || lower(@q) || '%'
                    )
                    AND (
                        @level IS NULL
                        OR level = @level
                    )
                ORDER BY
                    CASE
                        WHEN normalized_name = lower(@q) THEN 0
                        WHEN normalized_name ILIKE lower(@q) || '%' THEN 1
                        WHEN level_name = lower(@q) THEN 2
                        ELSE 3
                    END,
                    level,
                    name
                LIMIT @limit
                """;

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                new NpgsqlParameter<string>("q", search),
                new NpgsqlParameter<int?>("level", level)
                {
                    IsNullable = true
                },
                new NpgsqlParameter<int>("limit", take)
            );

            var response = rows.Select(row =>
                new LocationSearchResponse(
                    Id: (string)row["id"]!,
                    Code: (string)row["code"]!,
                    Name: (string)row["name"]!,
                    Level: Convert.ToInt32(row["level"]),
                    LevelName: (string)row["level_name"]!,
                    Latitude: Convert.ToDouble(row["latitude"]),
                    Longitude: Convert.ToDouble(row["longitude"]),
                    ParentId: row["parent_id"] as string))
                .ToList();

            return Results.Ok(response);
        })
        .WithTags("Locations")
        .WithName("SearchLocations")
        .WithSummary("Search Kenya administrative locations")
        .WithDescription("Searches counties, sub-counties, and wards by name or administrative level.")
        .Produces<List<LocationSearchResponse>>(StatusCodes.Status200OK)
        .Produces<ApiError>(StatusCodes.Status400BadRequest);

        app.MapGet("/api/v1/locations/{id}", async (
            [Description("Administrative location identifier.")]
            string id,
            NpgsqlDataSource db) =>
        {
            const string sql = """
                SELECT
                    a.id,
                    a.code,
                    a.name,
                    a.level,
                    a.level_name,
                    a.parent_id,
                    p.id AS parent_unit_id,
                    p.code AS parent_unit_code,
                    p.name AS parent_unit_name,
                    p.level AS parent_unit_level,
                    p.level_name AS parent_unit_level_name,
                    ST_Y(a.centroid::geometry) AS latitude,
                    ST_X(a.centroid::geometry) AS longitude,
                    a.source,
                    a.source_version
                FROM admin_units a
                LEFT JOIN admin_units p
                    ON p.id = a.parent_id
                WHERE a.id = @id
                LIMIT 1
                """;

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                new NpgsqlParameter<string>("id", id)
            );

            if (rows.Count == 0)
            {
                return ApiResults.NotFound(
                    "Location not found.",
                    id
                );
            }

            var row = rows[0];

            LocationParentResponse? parent = row["parent_unit_id"] is null
                ? null
                : new LocationParentResponse(
                    Id: (string)row["parent_unit_id"]!,
                    Code: (string)row["parent_unit_code"]!,
                    Name: (string)row["parent_unit_name"]!,
                    Level: Convert.ToInt32(row["parent_unit_level"]),
                    LevelName: (string)row["parent_unit_level_name"]!);

            var response = new LocationDetailResponse(
                Id: (string)row["id"]!,
                Code: (string)row["code"]!,
                Name: (string)row["name"]!,
                Level: Convert.ToInt32(row["level"]),
                LevelName: (string)row["level_name"]!,
                Parent: parent,
                Latitude: Convert.ToDouble(row["latitude"]),
                Longitude: Convert.ToDouble(row["longitude"]),
                Source: (string)row["source"]!,
                SourceVersion: (string)row["source_version"]!);

            return Results.Ok(response);
        })
        .WithTags("Locations")
        .WithName("GetLocation")
        .WithSummary("Get an administrative location")
        .WithDescription("Returns details for a county, sub-county, or ward by its identifier.")
        .Produces<LocationDetailResponse>(StatusCodes.Status200OK)
        .Produces<ApiNotFoundError>(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/locations/{id}/hierarchy", async (
            [Description("Administrative location identifier.")]
            string id,
            NpgsqlDataSource db) =>
        {
            if (string.IsNullOrWhiteSpace(id))
                return ApiResults.BadRequest(
                    "Location id is required.",
                    "MISSING_ID"
                );

            const string sql = """
                WITH RECURSIVE tree AS (
                    SELECT
                        id,
                        code,
                        name,
                        level,
                        level_name,
                        parent_id,
                        0 AS depth
                    FROM admin_units
                    WHERE id = @id

                    UNION ALL

                    SELECT
                        p.id,
                        p.code,
                        p.name,
                        p.level,
                        p.level_name,
                        p.parent_id,
                        t.depth + 1
                    FROM admin_units p
                    JOIN tree t
                        ON t.parent_id = p.id
                )
                SELECT
                    id,
                    code,
                    name,
                    level,
                    level_name,
                    parent_id,
                    depth
                FROM tree
                ORDER BY depth DESC
                """;

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                new NpgsqlParameter<string>("id", id.Trim())
            );

            if (rows.Count == 0)
            {
                return ApiResults.NotFound(
                    "Location not found.",
                    id
                );
            }

            var response = rows.Select(row =>
                new LocationHierarchyItemResponse(
                    Id: (string)row["id"]!,
                    Code: (string)row["code"]!,
                    Name: (string)row["name"]!,
                    Level: Convert.ToInt32(row["level"]),
                    LevelName: (string)row["level_name"]!,
                    ParentId: row["parent_id"] as string,
                    Depth: Convert.ToInt32(row["depth"])))
                .ToList();

            return Results.Ok(response);
        })
        .WithTags("Locations")
        .WithName("GetLocationHierarchy")
        .WithSummary("Get the administrative hierarchy")
        .WithDescription("Returns the county-to-ward hierarchy containing the requested administrative location.")
        .Produces<List<LocationHierarchyItemResponse>>(StatusCodes.Status200OK)
        .Produces<ApiError>(StatusCodes.Status400BadRequest)
        .Produces<ApiNotFoundError>(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/nearby", async (
            [Description("Latitude in decimal degrees. Valid range: -90 to 90.")]
            [FromQuery(Name = "latitude")] double latitude,

            [Description("Longitude in decimal degrees. Valid range: -180 to 180.")]
            [FromQuery(Name = "longitude")] double longitude,

            [Description("Search radius in kilometres. Default: 25. Valid range: 0.1 to 250.")]
            [FromQuery(Name = "radiusKm")] double? radiusKm,

            [Description("Maximum number of results to return. Default: 20. Valid range: 1 to 100.")]
            [FromQuery(Name = "limit")] int? limit,
            NpgsqlDataSource db) =>
        {
            if (latitude is < -90 or > 90)
            {
                return ApiResults.BadRequest(
                    "Latitude must be between -90 and 90.",
                    "INVALID_LATITUDE");
            }

            if (longitude is < -180 or > 180)
            {
                return ApiResults.BadRequest(
                    "Longitude must be between -180 and 180.",
                    "INVALID_LONGITUDE");
            }

            var radius = Math.Clamp(
                radiusKm ?? 25,
                0.1,
                250) * 1000;

            var take = Math.Clamp(limit ?? 20, 1, 100);

            const string sql = """
                WITH p AS (
                    SELECT ST_SetSRID(
                        ST_MakePoint(@lon, @lat),
                        4326
                    )::geography AS point
                )
                SELECT
                    a.id,
                    a.code,
                    a.name,
                    a.level,
                    a.level_name,
                    ST_Distance(
                        a.centroid::geography,
                        p.point
                    ) AS distance_meters,
                    ST_Y(a.centroid::geometry) AS latitude,
                    ST_X(a.centroid::geometry) AS longitude
                FROM admin_units a
                CROSS JOIN p
                WHERE ST_DWithin(
                    a.centroid::geography,
                    p.point,
                    @radius
                )
                ORDER BY a.centroid::geography <-> p.point
                LIMIT @limit
                """;

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                new NpgsqlParameter("lat", latitude),
                new NpgsqlParameter("lon", longitude),
                new NpgsqlParameter("radius", radius),
                new NpgsqlParameter("limit", take)
            );

            var response = rows.Select(row =>
                new NearbyLocationResponse(
                    Id: (string)row["id"]!,
                    Code: (string)row["code"]!,
                    Name: (string)row["name"]!,
                    Level: Convert.ToInt32(row["level"]),
                    LevelName: (string)row["level_name"]!,
                    DistanceMeters: Convert.ToDouble(row["distance_meters"]),
                    Latitude: Convert.ToDouble(row["latitude"]),
                    Longitude: Convert.ToDouble(row["longitude"])))
                .ToList();

            return Results.Ok(response);
        })
        .WithTags("Locations")
        .WithName("GetNearbyLocations")
        .WithSummary("Find nearby administrative locations")
        .WithDescription("Finds administrative locations near a latitude and longitude using a radius in kilometres.")
        .Produces<List<NearbyLocationResponse>>(StatusCodes.Status200OK)
        .Produces<ApiError>(StatusCodes.Status400BadRequest);
    }
}