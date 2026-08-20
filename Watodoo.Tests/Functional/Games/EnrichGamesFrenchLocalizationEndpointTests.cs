using System.Net;

namespace Watodoo.Tests.Functional.Games;

[Collection(nameof(SeedGamesFromIgdbTestCollection))]
public sealed class EnrichGamesFrenchLocalizationEndpointTests(SeedGamesFromIgdbTestFixture fixture)
{
    private const string Url = "/internal/ingestion/games/enrich-french";
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
    public async Task Returns_202_with_correct_admin_key_and_default_batchSize()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", ValidAdminKey);

        var response = await client.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Returns_400_for_batchSize_out_of_range(int batchSize)
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", ValidAdminKey);

        var response = await client.PostAsync($"{Url}?batchSize={batchSize}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
