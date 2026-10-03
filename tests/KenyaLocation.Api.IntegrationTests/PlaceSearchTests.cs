using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class PlaceSearchTests : IntegrationTestBase
{
    [Fact]
    public async Task Search_Kakamega_ReturnsUrbanArea()
    {
        var response = await Client.GetAsync(
            "/api/v1/places/search?q=Kakamega");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        var results = document.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(results);

        var kakamega = results.FirstOrDefault(result =>
            string.Equals(
                result.GetProperty("name").GetString(),
                "Kakamega",
                StringComparison.OrdinalIgnoreCase));

        Assert.False(kakamega.ValueKind == JsonValueKind.Undefined);

        Assert.Equal(
            "urban_area",
            kakamega.GetProperty("placeType").GetString());

        Assert.True(
            kakamega.TryGetProperty("latitude", out _));

        Assert.True(
            kakamega.TryGetProperty("longitude", out _));
    }
}
