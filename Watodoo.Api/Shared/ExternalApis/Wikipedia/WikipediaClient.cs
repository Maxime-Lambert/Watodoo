namespace Watodoo.Shared.ExternalApis.Wikipedia;

public sealed class WikipediaClient(HttpClient httpClient) : IWikipediaClient
{
    // Limite conservatrice pour titles= sur une requête anonyme (non authentifiée) — voir
    // plans/active-plan.md (hypothèses techniques), à ajuster si l'API répond une erreur de type
    // "too many titles".
    private const int BatchSize = 20;

    public async Task<Dictionary<string, string>> GetExtractsAsync(IReadOnlyCollection<string> titles, CancellationToken ct)
    {
        var extracts = new Dictionary<string, string>();

        foreach (var batch in titles.Distinct().Chunk(BatchSize))
        {
            await FetchBatchAsync(batch, extracts, ct);
        }

        return extracts;
    }

    private async Task FetchBatchAsync(string[] batch, Dictionary<string, string> extracts, CancellationToken ct)
    {
        var titlesParam = Uri.EscapeDataString(string.Join('|', batch));
        var url = $"w/api.php?action=query&prop=extracts&exintro=1&explaintext=1&redirects=1&formatversion=2&format=json&titles={titlesParam}";

        var response = await httpClient.GetFromJsonAsync<WikipediaQueryResponseDto>(url, ct);
        if (response?.Query is null)
        {
            return;
        }

        // Reconstruit, pour chaque titre demandé, le titre final effectivement renvoyé par l'API
        // (après normalisation Unicode puis suivi de redirection), pour retrouver son extrait — les
        // pages sont indexées par leur titre final, pas par le titre demandé.
        var finalTitleByRequested = batch.ToDictionary(title => title, title => title);
        Rewrite(finalTitleByRequested, response.Query.Normalized);
        Rewrite(finalTitleByRequested, response.Query.Redirects);

        var pageByFinalTitle = response.Query.Pages
            .Where(p => !p.Missing)
            .ToDictionary(p => p.Title);

        foreach (var (requested, finalTitle) in finalTitleByRequested)
        {
            if (pageByFinalTitle.TryGetValue(finalTitle, out var page) && !string.IsNullOrWhiteSpace(page.Extract))
            {
                extracts[requested] = page.Extract;
            }
        }
    }

    private static void Rewrite(Dictionary<string, string> finalTitleByRequested, List<WikipediaTitleMappingDto> mappings)
    {
        foreach (var mapping in mappings)
        {
            foreach (var requested in finalTitleByRequested.Keys.Where(k => finalTitleByRequested[k] == mapping.From).ToList())
            {
                finalTitleByRequested[requested] = mapping.To;
            }
        }
    }
}
