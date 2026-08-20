using Watodoo.Shared.ExternalApis.Igdb;

namespace Watodoo.Tests.Integration.Games;

public sealed class FakeIgdbClient : IIgdbClient
{
    private readonly Dictionary<int, List<IgdbGameDto>> _pagesByOffset = [];

    public List<IgdbQuery> ReceivedQueries { get; } = [];

    public void SetPage(int offset, List<IgdbGameDto> results) => _pagesByOffset[offset] = results;

    public Task<List<IgdbGameDto>> DiscoverGamesAsync(IgdbQuery query, CancellationToken ct)
    {
        ReceivedQueries.Add(query);

        var results = _pagesByOffset.GetValueOrDefault(query.Offset) ?? [];
        return Task.FromResult(results);
    }
}
