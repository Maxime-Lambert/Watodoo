namespace Watodoo.Shared.ExternalApis.Igdb;

public interface IIgdbClient
{
    Task<List<IgdbGameDto>> DiscoverGamesAsync(IgdbQuery query, CancellationToken ct);
}
