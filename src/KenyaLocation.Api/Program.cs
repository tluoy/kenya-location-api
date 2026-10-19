using Npgsql;
using KenyaLocation.Api.Endpoints;
using KenyaLocation.Api.Database;
using KenyaLocation.Api.Models;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


var permitLimit = builder.Configuration.GetValue<int?>(
    "RateLimiting:PermitLimit") ?? 60;

var windowSeconds = builder.Configuration.GetValue<int?>(
    "RateLimiting:WindowSeconds") ?? 60;

if (permitLimit <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:PermitLimit must be greater than zero.");
}

if (windowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:WindowSeconds must be greater than zero.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = permitLimit;
        limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{

    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Kenya Location Intelligence API",
        Version = "v1",
        Description = """
        A geospatial API for applications operating in Kenya.

        Capabilities:
        - Browse counties, sub-counties, and wards.
        - Search administrative locations and urban areas.
        - Retrieve location and place details.
        - Find nearby locations and places using geographic coordinates.
        - Reverse geolocate coordinates to administrative areas.
        - Inspect service health and administrative data coverage.

        The v1 value identifies the API contract version.
        The service release version is reported separately by /api/v1/meta.
        """
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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}. TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier
            );

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            error = new
            {
                code = "INTERNAL_SERVER_ERROR",
                message = "An unexpected error occurred."
            }
        });
    });
});

app.UseRateLimiter();

app.MapLocationEndpoints();
app.MapPlaceEndpoints();
app.MapGeolocationEndpoints();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health/live", () =>
{
    return Results.Ok(new
    {
        status = "alive",
        utc = DateTimeOffset.UtcNow
    });
})
.WithTags("Health")
.WithName("GetLiveness")
.Produces(StatusCodes.Status200OK)
.WithSummary("Check API liveness")
.WithDescription(
    "Returns HTTP 200 when the API process can respond to requests.");

app.MapGet("/health/ready", async (NpgsqlDataSource db) =>
{
    try
    {
        await using var cmd = db.CreateCommand("SELECT PostGIS_Version()");

        var version = await cmd.ExecuteScalarAsync();

        if (version is null || version is DBNull)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            status = "ready",
            postgis = Convert.ToString(version),
            utc = DateTimeOffset.UtcNow
        });
    }
    catch (NpgsqlException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
})
.WithTags("Health")
.WithName("GetReadiness")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status503ServiceUnavailable)
.WithSummary("Check API readiness")
.WithDescription(
    "Checks database connectivity and PostGIS availability. " +
    "Returns HTTP 503 when the readiness check fails.");

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
.Produces(StatusCodes.Status200OK)
.WithSummary("Check API and database health")
.WithDescription(
    "Returns the legacy health response, including the PostGIS version.");

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