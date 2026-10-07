namespace KenyaLocation.Api.IntegrationTests;

public abstract class IntegrationTestBase
{
    protected HttpClient Client { get; }

    protected IntegrationTestBase()
    {
        Client = new HttpClient
        {
            BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("API_BASE_URL")
                ?? "http://localhost:8080")
        };
    }
}
