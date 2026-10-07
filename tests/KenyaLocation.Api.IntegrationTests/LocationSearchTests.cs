using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class LocationSearchTests : IntegrationTestBase
{
    [Fact]
    public async Task Search_Kakamega_ReturnsMatchingLocation()
    {
        var response = await Client.GetAsync(
            "/api/v1/locations/search?q=Kakamega");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        var results = document.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(results);

        Assert.Contains(
            results,
            result =>
                string.Equals(
                    result.GetProperty("name").GetString(),
                    "Kakamega",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_UnknownLocation_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync(
            "/api/v1/locations/search?q=THIS_LOCATION_DOES_NOT_EXIST");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Empty(document.RootElement.EnumerateArray());
    }
}