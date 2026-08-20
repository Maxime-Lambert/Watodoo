using System.Net;

namespace Watodoo.Tests.Functional.Games;

[Collection(nameof(SeedGamesFromIgdbTestCollection))]
public sealed class SeedGamesFromIgdbEndpointTests(SeedGamesFromIgdbTestFixture fixture)
{
    private const string Url = "/internal/ingestion/games/igdb";
    private const string ValidAdminKey = "test-only-admin-key-1234567890";

    [Fact]
    public async Task Returns_404_without_admin_key_header()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_with_wrong_admin_key_header()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", "mauvaise-cle");

        var response = await client.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_202_with_correct_admin_key_and_default_maxItems()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", ValidAdminKey);

        var response = await client.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50_001)]
    public async Task Returns_400_for_maxItems_out_of_range(int maxItems)
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", ValidAdminKey);

        var response = await client.PostAsync($"{Url}?maxItems={maxItems}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
