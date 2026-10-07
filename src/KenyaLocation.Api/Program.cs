using Npgsql;
using KenyaLocation.Api.Endpoints;
using KenyaLocation.Api.Database;
using KenyaLocation.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Kenya Location Intelligence API",
        Version = "v1",
        Description = "Geospatial API for Kenya administrative boundaries, places, nearby searches, and reverse geolocation."
    });

    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Postgres connection string is not configured.");
    }

    return new NpgsqlDataSourceBuilder(connectionString).Build();
});

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
})
.WithTags("Health")
.WithName("GetHealth")
.Produces(StatusCodes.Status200OK);

app.MapGet("/api/v1/meta", async (NpgsqlDataSource db) =>
{
    const string sql = """
        SELECT level, level_name, count(*) AS count
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
})
.WithTags("Meta")
.WithName("GetMeta")
.WithSummary("Get API metadata")
.WithDescription("Returns service metadata and counts for the supported administrative levels.")
.Produces<MetaResponse>(StatusCodes.Status200OK);

app.Run();