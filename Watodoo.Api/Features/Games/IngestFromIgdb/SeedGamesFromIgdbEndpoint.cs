using Hangfire;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;
using Watodoo.Shared.Security;

namespace Watodoo.Features.Games.IngestFromIgdb;

public static class SeedGamesFromIgdbEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/igdb", (
            int? maxItems,
            IOptions<IgdbOptions> options,
            IBackgroundJobClient jobClient) =>
        {
            var items = maxItems ?? options.Value.SeedMaxItems;
            if (items is < 1 or > 50_000)
            {
                return Results.BadRequest("maxItems doit être entre 1 et 50000.");
            }

            var jobId = jobClient.Enqueue<IngestGamesFromIgdbJob>(job => job.SeedAsync(items, CancellationToken.None));
            return Results.Accepted(value: new { jobId });
        }).AddEndpointFilter<AdminKeyEndpointFilter>();
    }
}
