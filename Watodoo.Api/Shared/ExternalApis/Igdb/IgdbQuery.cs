namespace Watodoo.Shared.ExternalApis.Igdb;

public sealed record IgdbQuery(int Offset, int Limit = 500, DateOnly? ReleaseDateFrom = null, DateOnly? ReleaseDateTo = null)
{
    // Pas de champ "popularity" natif sur l'endpoint /games — total_rating_count est le meilleur
    // proxy disponible (nombre d'avis, corrélé à la notoriété). Voir plans/active-plan.md (hypothèses
    // techniques) : à confirmer contre https://api-docs.igdb.com/#game si ce champ ne convient pas.
    public string SortBy { get; init; } = "total_rating_count desc";
}
