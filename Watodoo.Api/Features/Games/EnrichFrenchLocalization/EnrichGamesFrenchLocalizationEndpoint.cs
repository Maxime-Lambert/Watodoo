using Hangfire;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;
using Watodoo.Shared.Security;

namespace Watodoo.Features.Games.EnrichFrenchLocalization;

public static class EnrichGamesFrenchLocalizationEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/enrich-french", (
            int? batchSize,
            IOptions<GamesOptions> options,
            IBackgroundJobClient jobClient) =>
        {
            var size = batchSize ?? options.Value.FrenchEnrichmentBatchSize;
            if (size is < 1 or > 1000)
            {
                return Results.BadRequest("batchSize doit être entre 1 et 1000.");
            }

            var jobId = jobClient.Enqueue<EnrichGamesFrenchLocalizationJob>(job => job.RunAsync(size, CancellationToken.None));
            return Results.Accepted(value: new { jobId });
        }).AddEndpointFilter<AdminKeyEndpointFilter>();
    }
}
