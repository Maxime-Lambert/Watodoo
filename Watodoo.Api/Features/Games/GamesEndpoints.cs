using Watodoo.Features.Games.EnrichFrenchLocalization;
using Watodoo.Features.Games.IngestFromIgdb;

namespace Watodoo.Features.Games;

public static class GamesEndpoints
{
    public static void MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/ingestion/games");

        SeedGamesFromIgdbEndpoint.Map(group);
        EnrichGamesFrenchLocalizationEndpoint.Map(group);
    }
}
