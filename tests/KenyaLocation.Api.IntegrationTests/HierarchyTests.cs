using System.Net;
using System.Text.Json;

namespace KenyaLocation.Api.IntegrationTests;

public class HierarchyTests : IntegrationTestBase
{
    [Fact]
    public async Task Sheywe_Hierarchy_ReturnsExpectedAdministrativeChain()
    {
        const string wardId = "KE-L3-90231094b30831898281931";

        var response = await Client.GetAsync(
            $"/api/v1/locations/{wardId}/hierarchy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        var hierarchy = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(3, hierarchy.Count);

        Assert.Equal(
            "county",
            hierarchy[0].GetProperty("levelName").GetString());

        Assert.Equal(
            "Kakamega",
            hierarchy[0].GetProperty("name").GetString());

        Assert.Equal(
            "sub-county",
            hierarchy[1].GetProperty("levelName").GetString());

        Assert.Equal(
            "Lurambi",
            hierarchy[1].GetProperty("name").GetString());

        Assert.Equal(
            "ward",
            hierarchy[2].GetProperty("levelName").GetString());

        Assert.Equal(
            "Sheywe",
            hierarchy[2].GetProperty("name").GetString());
    }
}
