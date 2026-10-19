
using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public sealed class HealthCheckTests : IntegrationTestBase
{
    [Fact]
    public async Task Liveness_ReturnsOkAndAliveStatus()
    {
        var response = await Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "alive",
            json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Readiness_ReturnsExpectedStatus()
    {
        var response = await Client.GetAsync("/health/ready");

        Assert.Contains(
            response.StatusCode,
            new[]
            {
                HttpStatusCode.OK,
                HttpStatusCode.ServiceUnavailable
            });

        if (response.StatusCode == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            Assert.Equal(
                "ready",
                json.RootElement.GetProperty("status").GetString());

            Assert.True(
                json.RootElement.TryGetProperty("postgis", out _));
        }
    }

    [Fact]
    public async Task LegacyHealthEndpoint_RemainsAvailable()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "ok",
            json.RootElement.GetProperty("status").GetString());

        Assert.True(
            json.RootElement.TryGetProperty("postgis", out _));
    }
}
