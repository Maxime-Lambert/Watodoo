using Watodoo.Shared.ExternalApis.Wikidata;

namespace Watodoo.Tests.Integration.Games;

public sealed class FakeWikidataClient : IWikidataClient
{
    private readonly Dictionary<string, WikidataMatchDto> _matchesByTitle = [];

    public List<string> ReceivedTitles { get; } = [];

    public void SetMatch(string title, WikidataMatchDto match) => _matchesByTitle[title] = match;

    public Task<WikidataMatchDto?> FindByTitleAsync(string title, DateOnly? releaseDate, CancellationToken ct)
    {
        ReceivedTitles.Add(title);

        return Task.FromResult(_matchesByTitle.GetValueOrDefault(title));
    }
}
