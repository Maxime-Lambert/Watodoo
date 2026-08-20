namespace Watodoo.Shared.ExternalApis.Wikipedia;

public interface IWikipediaClient
{
    // Résultat indexé par le titre demandé (pas le titre final après normalisation/redirection) :
    // une clé absente signifie "extrait introuvable pour ce titre", pas une erreur.
    Task<Dictionary<string, string>> GetExtractsAsync(IReadOnlyCollection<string> titles, CancellationToken ct);
}
