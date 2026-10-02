using KenyaLocation.Api.Database;
using Npgsql;
using KenyaLocation.Api.Models;
using System.Text.Json;

namespace KenyaLocation.Api.Endpoints;

public static class PlaceEndpoints
{
    public static void MapPlaceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/places/search", async (
            string? q,
            string? place_type,
            int? limit,
            NpgsqlDataSource db) =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 100);

            var search = q?.Trim() ?? "";
            var type = place_type?.Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(type) &&
                type is not ("urban_area" or "settlement"))
            {
                return ApiResults.BadRequest(
                    "place_type must be 'urban_area' or 'settlement'.",
                    "INVALID_PLACE_TYPE"
                );
            }

            const string sql = """
                SELECT
                    p.id,
                    p.name,
                    p.place_type,
                    ST_Y(p.centroid::geometry) AS latitude,
                    ST_X(p.centroid::geometry) AS longitude,
                    w.name AS ward,
                    s.name AS sub_county,
                    c.name AS county,
                    p.admin_unit_id
                FROM places p
                LEFT JOIN admin_units w
                    ON w.id = p.admin_unit_id
                   AND w.level = 3
                LEFT JOIN admin_units s
                    ON s.id = w.parent_id
                   AND s.level = 2
                LEFT JOIN admin_units c
                    ON c.id = s.parent_id
                   AND c.level = 1
                WHERE
                    (
                        @q = ''
                        OR p.normalized_name ILIKE '%' || lower(@q) || '%'
                        OR p.place_type ILIKE '%' || lower(@q) || '%'
                    )
                    AND (
                        @place_type IS NULL
                        OR p.place_type = @place_type
                    )
                ORDER BY
                    CASE
                        WHEN p.normalized_name = lower(@q) THEN 0
                        WHEN p.normalized_name ILIKE lower(@q) || '%' THEN 1
                        ELSE 2
                    END,
                    p.name
                LIMIT @limit
                """;

            var qParameter = new NpgsqlParameter<string>("q", search);

            var typeParameter = new NpgsqlParameter<string?>(
                "place_type",
                string.IsNullOrEmpty(type) ? null : type)
            {
                IsNullable = true
            };

            var limitParameter = new NpgsqlParameter<int>("limit", take);

            var rows = await DbQuery.QueryRows(db, sql, qParameter, typeParameter, limitParameter);

            var response = rows.Select(row =>
                new PlaceSearchResponse(
                    Id: (string)row["id"]!,
                    Name: (string)row["name"]!,
                    PlaceType: (string)row["place_type"]!,
                    Latitude: Convert.ToDouble(row["latitude"]),
                    Longitude: Convert.ToDouble(row["longitude"]),
                    Ward: row["ward"] as string,
                    SubCounty: row["sub_county"] as string,
                    County: row["county"] as string,
                    AdminUnitId: row["admin_unit_id"] as string))
                .ToList();

            return Results.Ok(response);
        });

        app.MapGet("/api/v1/places/{id}", async (
            string id,
            NpgsqlDataSource db) =>
        {
            const string sql = """
                SELECT
                    p.id,
                    p.name,
                    p.place_type,
                    ST_Y(p.centroid::geometry) AS latitude,
                    ST_X(p.centroid::geometry) AS longitude,
                    ST_AsGeoJSON(p.geometry::geometry)::jsonb AS geometry,
                    w.id AS ward_id,
                    w.name AS ward,
                    s.id AS sub_county_id,
                    s.name AS sub_county,
                    c.id AS county_id,
                    c.name AS county,
                    p.admin_unit_id,
                    p.source,
                    p.source_version
                FROM places p
                LEFT JOIN admin_units w
                    ON w.id = p.admin_unit_id
                   AND w.level = 3
                LEFT JOIN admin_units s
                    ON s.id = w.parent_id
                   AND s.level = 2
                LEFT JOIN admin_units c
                    ON c.id = s.parent_id
                   AND c.level = 1
                WHERE p.id = @id
                LIMIT 1
                """;

            var idParameter = new NpgsqlParameter<string>("id", id);

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                idParameter
            );

            if (rows.Count == 0)
            {
                return ApiResults.NotFound(
                    "Place not found.",
                    id
                );
            }

            var row = rows[0];

            if (row.TryGetValue("geometry", out var geometry) && geometry is string geometryJson)
            {
                geometry = JsonSerializer.Deserialize<JsonElement>(geometryJson);
            }

            var response = new PlaceDetailResponse(
                Id: (string)row["id"]!,
                Name: (string)row["name"]!,
                PlaceType: (string)row["place_type"]!,
                Latitude: Convert.ToDouble(row["latitude"]),
                Longitude: Convert.ToDouble(row["longitude"]),
                Geometry: geometry!,
                WardId: row["ward_id"] as string,
                Ward: row["ward"] as string,
                SubCountyId: row["sub_county_id"] as string,
                SubCounty: row["sub_county"] as string,
                CountyId: row["county_id"] as string,
                County: row["county"] as string,
                AdminUnitId: row["admin_unit_id"] as string,
                Source: (string)row["source"]!,
                SourceVersion: (string)row["source_version"]!);

            return Results.Ok(response);
        });

        app.MapGet("/api/v1/places/nearby", async (
            double latitude,
            double longitude,
            double? radius_km,
            int? limit,
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

            var radiusKm = Math.Clamp(
                radius_km ?? 25,
                0.1,
                500);

            var take = Math.Clamp(
                limit ?? 20,
                1,
                100);

            const string sql = """
                WITH point AS (
                    SELECT ST_SetSRID(
                        ST_MakePoint(@longitude, @latitude),
                        4326
                    )::geography AS geom
                )
                SELECT
                    p.id,
                    p.name,
                    p.place_type,
                    ST_Y(p.centroid::geometry) AS latitude,
                    ST_X(p.centroid::geometry) AS longitude,
                    ST_Distance(
                        p.centroid::geography,
                        point.geom
                    ) AS distance_meters,
                    w.name AS ward,
                    s.name AS sub_county,
                    c.name AS county
                FROM places p
                CROSS JOIN point
                LEFT JOIN admin_units w
                    ON w.id = p.admin_unit_id
                AND w.level = 3
                LEFT JOIN admin_units s
                    ON s.id = w.parent_id
                AND s.level = 2
                LEFT JOIN admin_units c
                    ON c.id = s.parent_id
                AND c.level = 1
                WHERE ST_DWithin(
                    p.centroid::geography,
                    point.geom,
                    @radius_meters
                )
                ORDER BY distance_meters
                LIMIT @limit
                """;

            var latitudeParameter =
                new NpgsqlParameter<double>("latitude", latitude);

            var longitudeParameter =
                new NpgsqlParameter<double>("longitude", longitude);

            var radiusParameter =
                new NpgsqlParameter<double>(
                    "radius_meters",
                    radiusKm * 1000);

            var limitParameter =
                new NpgsqlParameter<int>("limit", take);

            var rows = await DbQuery.QueryRows(
                db,
                sql,
                latitudeParameter,
                longitudeParameter,
                radiusParameter,
                limitParameter);

            var response = rows.Select(row =>
                new NearbyPlaceResponse(
                    Id: (string)row["id"]!,
                    Name: (string)row["name"]!,
                    PlaceType: (string)row["place_type"]!,
                    Latitude: Convert.ToDouble(row["latitude"]),
                    Longitude: Convert.ToDouble(row["longitude"]),
                    DistanceMeters: Convert.ToDouble(row["distance_meters"]),
                    Ward: row["ward"] as string,
                    SubCounty: row["sub_county"] as string,
                    County: row["county"] as string))
                .ToList();

            return Results.Ok(response);
        });
    }
}