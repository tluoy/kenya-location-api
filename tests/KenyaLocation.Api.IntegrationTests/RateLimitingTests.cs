
using System.Net;

namespace KenyaLocation.Api.IntegrationTests;

public class RateLimitingTests : IntegrationTestBase
{
    [Fact]
    public async Task Counties_ExceedingRateLimit_ReturnsTooManyRequests()
    {
        for (var request = 0; request < 2; request++)
        {
            using var response = await Client.GetAsync(
                "/api/v1/locations/counties");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }

        using var rejectedResponse = await Client.GetAsync(
            "/api/v1/locations/counties");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejectedResponse.StatusCode);

        using var healthResponse = await Client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            healthResponse.StatusCode);
    }
}
