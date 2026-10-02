using Npgsql;
using KenyaLocation.Api.Endpoints;
using KenyaLocation.Api.Database;
using KenyaLocation.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(sp => new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("Postgres") ??
    "Host=localhost;Port=5432;Database=kenya_location;Username=location;Password=REMOVED_CREDENTIAL")
    .Build());

var app = builder.Build();

app.MapLocationEndpoints();
app.MapPlaceEndpoints();
app.MapGeolocationEndpoints();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (NpgsqlDataSource db) =>
{
    await using var cmd = db.CreateCommand("SELECT PostGIS_Version() AS postgis_version");
    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    return Results.Ok(new
    {
        status = "ok",
        postgis = reader.GetString(0),
        utc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/v1/meta", async (NpgsqlDataSource db) =>
{
    const string sql = """
        SELECT
            level,
            level_name,
            count(*) AS count
        FROM admin_units
        GROUP BY level, level_name
        ORDER BY level
        """;

    var rows = await DbQuery.QueryRows(db, sql);

    var levels = rows
        .Select(row => new MetaLevelResponse(
            Level: Convert.ToInt32(row["level"]),
            LevelName: (string)row["level_name"]!,
            Count: Convert.ToInt32(row["count"])))
        .ToList();

    var response = new MetaResponse(
        Service: "Kenya Location Intelligence API",
        Version: "0.1.0-mvp",
        Levels: levels);

    return Results.Ok(response);
});

app.Run();