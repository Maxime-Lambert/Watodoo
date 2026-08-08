using Microsoft.EntityFrameworkCore;
using Watodoo.Shared.Data;

namespace Watodoo.Features.Auth.CleanupExpiredRefreshTokens;

public sealed class CleanupExpiredRefreshTokensJob(WatodooDbContext db, ILogger<CleanupExpiredRefreshTokensJob> logger)
{
    // CancellationToken en paramètre (comme les autres handlers Auth) : Hangfire l'injecte
    // automatiquement avec le token d'arrêt du serveur à l'exécution, quelle que soit la valeur
    // capturée dans l'expression de RecurringJob.AddOrUpdate — permet une annulation coopérative
    // du DELETE en cours si le conteneur backend s'arrête pendant l'exécution du job.
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var deletedCount = await db.RefreshTokens
            .Where(rt => rt.RevokedAt != null || rt.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation(
            "Nettoyage refresh tokens : {DeletedCount} ligne(s) supprimée(s) (expirées ou révoquées).",
            deletedCount);

        return deletedCount;
    }
}
