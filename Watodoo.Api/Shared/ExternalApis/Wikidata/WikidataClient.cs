using System.Text.Json;

namespace Watodoo.Shared.ExternalApis.Wikidata;

public sealed class WikidataClient(HttpClient httpClient) : IWikidataClient
{
    private const string VideoGameQid = "Q7889";
    private const int MaxCandidates = 5;

    // Marge sur l'année de sortie : les dates de sortie IGDB/Wikidata peuvent différer de quelques
    // jours autour du nouvel an selon la région retenue par chaque source (ex. sortie le 30 décembre
    // côté IGDB, 2 janvier côté Wikidata pour la même édition) — 1 an de marge absorbe ce genre de cas
    // sans devenir permissif au point d'accepter un jeu sorti une décennie plus tard/plus tôt.
    private const int ReleaseYearTolerance = 1;

    public async Task<WikidataMatchDto?> FindByTitleAsync(string title, DateOnly? releaseDate, CancellationToken ct)
    {
        var candidateIds = await SearchCandidateIdsAsync(title, ct);
        if (candidateIds.Count == 0)
        {
            return null;
        }

        var entities = await GetEntitiesAsync(candidateIds, ct);

        // Ordre de pertinence renvoyé par la recherche Wikidata : on garde le premier candidat qui
        // passe les deux vérifications, pas nécessairement le tout premier résultat de recherche.
        foreach (var id in candidateIds)
        {
            if (!entities.TryGetValue(id, out var entity))
            {
                continue;
            }

            if (!IsVideoGame(entity) || !MatchesReleaseYear(entity, releaseDate))
            {
                continue;
            }

            var frTitle = entity.Labels.GetValueOrDefault("fr")?.Value;
            var frWikipediaTitle = entity.Sitelinks.GetValueOrDefault("frwiki")?.Title;

            return new WikidataMatchDto(id, frTitle, frWikipediaTitle);
        }

        return null;
    }

    private async Task<List<string>> SearchCandidateIdsAsync(string title, CancellationToken ct)
    {
        var url = $"w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(title)}&language=en&type=item&limit={MaxCandidates}&format=json";
        var response = await httpClient.GetFromJsonAsync<WikidataSearchResponseDto>(url, ct) ?? new WikidataSearchResponseDto();

        return response.Search.Select(r => r.Id).ToList();
    }

    private async Task<Dictionary<string, WikidataEntityDto>> GetEntitiesAsync(List<string> ids, CancellationToken ct)
    {
        var idsParam = string.Join('|', ids);
        var url = $"w/api.php?action=wbgetentities&ids={idsParam}&props=labels|claims|sitelinks&languages=en|fr&format=json";
        var response = await httpClient.GetFromJsonAsync<WikidataEntitiesResponseDto>(url, ct) ?? new WikidataEntitiesResponseDto();

        return response.Entities;
    }

    private static bool IsVideoGame(WikidataEntityDto entity) =>
        entity.Claims.GetValueOrDefault("P31")?.Any(claim => ReadEntityId(claim) == VideoGameQid) ?? false;

    // Pas de correspondance possible (pas de date IGDB, ou l'item Wikidata n'a aucune date de sortie
    // renseignée) : accepté par défaut plutôt que rejeté — le titre + le fait que ce soit bien un jeu
    // vidéo (IsVideoGame) restent le principal filtre dans ce cas, cette vérification est un garde-fou
    // best-effort, pas une exigence stricte.
    private static bool MatchesReleaseYear(WikidataEntityDto entity, DateOnly? releaseDate)
    {
        if (releaseDate is not { } date)
        {
            return true;
        }

        var wikidataYears = entity.Claims.GetValueOrDefault("P577")?
            .Select(ReadYear)
            .Where(year => year is not null)
            .Select(year => year!.Value)
            .ToList() ?? [];

        return wikidataYears.Count == 0 || wikidataYears.Any(year => Math.Abs(year - date.Year) <= ReleaseYearTolerance);
    }

    private static string? ReadEntityId(WikidataClaimDto claim)
    {
        var value = claim.MainSnak?.DataValue?.Value ?? default;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private static int? ReadYear(WikidataClaimDto claim)
    {
        var value = claim.MainSnak?.DataValue?.Value ?? default;
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("time", out var time))
        {
            return null;
        }

        // Format Wikidata : "+2015-05-19T00:00:00Z" (signe +/- obligatoire en préfixe).
        var raw = time.GetString();
        return raw is { Length: > 5 } && int.TryParse(raw.AsSpan(1, 4), out var year) ? year : null;
    }
}
