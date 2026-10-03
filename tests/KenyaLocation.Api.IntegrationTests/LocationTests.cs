using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class LocationTests : IntegrationTestBase
{
    [Fact]
    public async Task Counties_ReturnExpectedCount()
    {
        var response = await Client.GetAsync("/api/v1/locations/counties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.ValueKind == JsonValueKind.Array);

        var counties = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(47, counties.Count);
    }

    [Fact]
    public async Task GetUnknownLocation_ReturnsNotFound()
    {
        const string unknownId = "KE-L3-does-not-exist";

        var response = await Client.GetAsync(
            $"/api/v1/locations/{unknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
