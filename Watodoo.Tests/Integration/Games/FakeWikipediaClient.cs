using Watodoo.Shared.ExternalApis.Wikipedia;

namespace Watodoo.Tests.Integration.Games;

public sealed class FakeWikipediaClient : IWikipediaClient
{
    private readonly Dictionary<string, string> _extracts = [];

    public List<string> ReceivedTitles { get; } = [];

    public void SetExtract(string title, string extract) => _extracts[title] = extract;

    public Task<Dictionary<string, string>> GetExtractsAsync(IReadOnlyCollection<string> titles, CancellationToken ct)
    {
        ReceivedTitles.AddRange(titles);

        var found = titles
            .Where(_extracts.ContainsKey)
            .ToDictionary(title => title, title => _extracts[title]);
        return Task.FromResult(found);
    }
}
