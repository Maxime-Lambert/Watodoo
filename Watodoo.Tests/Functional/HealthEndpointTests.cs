using System.Net;

namespace Watodoo.Tests.Functional;

[Collection(nameof(FunctionalTestCollection))]
public sealed class HealthEndpointTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
