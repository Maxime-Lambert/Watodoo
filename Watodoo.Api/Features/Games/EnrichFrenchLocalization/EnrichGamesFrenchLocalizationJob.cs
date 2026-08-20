using Microsoft.EntityFrameworkCore;
using Watodoo.Shared.Data;
using Watodoo.Shared.ExternalApis.Wikidata;
using Watodoo.Shared.ExternalApis.Wikipedia;

namespace Watodoo.Features.Games.EnrichFrenchLocalization;

public sealed class EnrichGamesFrenchLocalizationJob(
    WatodooDbContext db,
    IWikidataClient wikidataClient,
    IWikipediaClient wikipediaClient,
    ILogger<EnrichGamesFrenchLocalizationJob> logger)
{
    // Traite un lot borné par exécution (comme NightlySafetyRequestCap côté IngestGamesFromIgdbJob) :
    // un gros backlog après un seed volumineux se rattrape sur plusieurs nuits plutôt qu'en un seul
    // run massif.
    public async Task RunAsync(int batchSize, CancellationToken ct = default)
    {
        var games = await db.Games
            .Where(g => g.FrenchEnrichedAt == null)
            .OrderBy(g => g.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        if (games.Count == 0)
        {
            logger.LogInformation("Enrichissement FR jeux : aucun jeu en attente.");
            return;
        }

        // Matching par titre (pas par IgdbId) : la propriété Wikidata "IGDB game ID" (P5794) s'est
        // révélée quasiment vide en vérification réelle, y compris pour des jeux très connus — voir
        // plans/active-plan.md. Un appel par jeu (recherche + détails), pas de requête groupée
        // possible côté API de recherche Wikidata.
        var now = DateTimeOffset.UtcNow;
        var matchedCount = 0;

        foreach (var game in games)
        {
            var match = await wikidataClient.FindByTitleAsync(game.TitleEn, game.ReleaseDate, ct);
            if (match is not null)
            {
                game.WikidataQid = match.QId;

                if (!string.IsNullOrWhiteSpace(match.FrTitle))
                {
                    game.TitleFr = match.FrTitle;
                }

                if (match.FrWikipediaTitle is { } title)
                {
                    var extracts = await wikipediaClient.GetExtractsAsync([title], ct);
                    if (extracts.TryGetValue(title, out var extract))
                    {
                        game.SynopsisFr = extract;
                    }
                }

                matchedCount++;
            }

            // Dans tous les cas (match ou non) : évite de retraiter indéfiniment les jeux sans
            // correspondance Wikidata. Limite acceptée, voir plans/active-plan.md (edge cases).
            game.FrenchEnrichedAt = now;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Enrichissement FR jeux : {Matched}/{Total} jeu(x) avec correspondance Wikidata.", matchedCount, games.Count);
    }
}
