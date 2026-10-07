using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class HealthTests : IntegrationTestBase
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "ok",
            document.RootElement.GetProperty("status").GetString());

        Assert.True(
            document.RootElement.TryGetProperty("postgis", out var postgis));

        Assert.False(string.IsNullOrWhiteSpace(postgis.GetString()));
    }
}
