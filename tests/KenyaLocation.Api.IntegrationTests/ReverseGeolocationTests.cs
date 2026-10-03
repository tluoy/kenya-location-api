using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class ReverseGeolocationTests : IntegrationTestBase
{
    [Fact]
    public async Task ReverseGeolocation_Kakamega_ReturnsExpectedHierarchy()
    {
        const double latitude = 0.2935415843267037;
        const double longitude = 34.751392720669116;

        var response = await Client.GetAsync(
            FormattableString.Invariant(
                $"/api/v1/geolocation/reverse?latitude={latitude}&longitude={longitude}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);

        var root = document.RootElement;

        Assert.Equal(
            latitude,
            root.GetProperty("latitude").GetDouble(),
            precision: 12);

        Assert.Equal(
            longitude,
            root.GetProperty("longitude").GetDouble(),
            precision: 12);

        var administrative = root.GetProperty("administrative");

        Assert.Equal(
            "Kakamega",
            administrative
                .GetProperty("county")
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            "Lurambi",
            administrative
                .GetProperty("subCounty")
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            "Sheywe",
            administrative
                .GetProperty("ward")
                .GetProperty("name")
                .GetString());

        var places = root.GetProperty("places");

        Assert.Equal(JsonValueKind.Array, places.ValueKind);

        Assert.Contains(
            places.EnumerateArray(),
            place =>
                string.Equals(
                    place.GetProperty("name").GetString(),
                    "Kakamega",
                    StringComparison.OrdinalIgnoreCase)
                &&
                string.Equals(
                    place.GetProperty("type").GetString(),
                    "urban_area",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReverseGeolocation_InvalidLatitude_ReturnsBadRequest()
    {
        var response = await Client.GetAsync(
            "/api/v1/geolocation/reverse?latitude=95&longitude=34.75");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
