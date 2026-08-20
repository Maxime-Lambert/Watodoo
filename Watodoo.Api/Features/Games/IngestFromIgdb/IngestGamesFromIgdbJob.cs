using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;
using Watodoo.Shared.Data;
using Watodoo.Shared.ExternalApis.Igdb;

namespace Watodoo.Features.Games.IngestFromIgdb;

public sealed class IngestGamesFromIgdbJob(
    WatodooDbContext db,
    IIgdbClient igdbClient,
    IOptions<IgdbOptions> options,
    ILogger<IngestGamesFromIgdbJob> logger)
{
    private const int PageSize = 500;

    // Filet de sécurité pour RefreshNightlyAsync : en pratique la fenêtre de sorties récentes tient
    // toujours sur 1-2 requêtes, ce plafond n'est là que pour éviter une boucle non bornée en cas
    // d'anomalie IGDB (page qui ne redevient jamais vide).
    private const int NightlySafetyRequestCap = 10;

    // Grand volume, déclenché à la demande (endpoint de seed) : parcourt les jeux triés par
    // popularité approximative jusqu'à maxItems. Idempotent (upsert par IgdbId), donc rejouable sans
    // risque.
    public async Task SeedAsync(int maxItems, CancellationToken ct = default)
    {
        var total = 0;

        for (var offset = 0; offset < maxItems; offset += PageSize)
        {
            var limit = Math.Min(PageSize, maxItems - offset);
            var count = await IngestPageAsync(new IgdbQuery(offset, limit), ct);
            if (count == 0)
            {
                break;
            }

            total += count;
        }

        logger.LogInformation(
            "Seed IGDB jeux : {Total} jeu(x) traité(s) (jusqu'à {MaxItems} demandé(s)).", total, maxItems);
    }

    // Petit volume, planifié nightly : uniquement les sorties récentes (fenêtre glissante), pas de
    // repasse sur la popularité — les jeux déjà en base ne sont donc rafraîchis (note, synopsis EN...)
    // que par un nouveau SeedAsync manuel, jamais par ce job.
    public async Task RefreshNightlyAsync(CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-options.Value.NightlyLookbackDays));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var total = 0;

        for (var request = 0; request < NightlySafetyRequestCap; request++)
        {
            var query = new IgdbQuery(request * PageSize, PageSize, ReleaseDateFrom: from, ReleaseDateTo: to);
            var count = await IngestPageAsync(query, ct);
            if (count == 0)
            {
                break;
            }

            total += count;
        }

        logger.LogInformation(
            "Refresh nightly IGDB jeux : {Total} jeu(x) traité(s) (sorties entre {From} et {To}).", total, from, to);
    }

    private async Task<int> IngestPageAsync(IgdbQuery query, CancellationToken ct)
    {
        var page = await igdbClient.DiscoverGamesAsync(query, ct);
        if (page.Count == 0)
        {
            return 0;
        }

        var igdbIds = page.Select(g => g.Id).ToList();
        var existingGames = await db.Games
            .Where(g => igdbIds.Contains(g.IgdbId))
            .ToDictionaryAsync(g => g.IgdbId, ct);

        foreach (var dto in page)
        {
            if (existingGames.TryGetValue(dto.Id, out var game))
            {
                GameMapper.ApplyTo(game, dto);
            }
            else
            {
                db.Games.Add(GameMapper.ToNewEntity(dto));
            }
        }

        await db.SaveChangesAsync(ct);

        return page.Count;
    }
}
