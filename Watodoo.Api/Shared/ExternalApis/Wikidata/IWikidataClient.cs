namespace Watodoo.Shared.ExternalApis.Wikidata;

public interface IWikidataClient
{
    // Recherche par titre (pas par ID externe — voir plans/active-plan.md : la propriété Wikidata
    // "IGDB game ID" (P5794) s'est révélée quasiment vide, y compris pour des jeux très connus).
    // releaseDate sert de garde-fou : un candidat trouvé par titre n'est retenu que s'il est
    // effectivement un jeu vidéo (P31) et que sa date de sortie Wikidata concorde avec IGDB, pour
    // éviter les faux-matchs entre jeux au titre proche. Retourne null si aucun candidat ne passe
    // ces deux vérifications.
    Task<WikidataMatchDto?> FindByTitleAsync(string title, DateOnly? releaseDate, CancellationToken ct);
}
