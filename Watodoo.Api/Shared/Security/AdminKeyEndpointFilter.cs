using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Watodoo.Configuration;

namespace Watodoo.Shared.Security;

public sealed class AdminKeyEndpointFilter(IOptions<IngestionOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var providedKey = context.HttpContext.Request.Headers["X-Admin-Key"].ToString();

        // Comparaison via le hash SHA-256 des deux valeurs plutôt que FixedTimeEquals directement sur
        // les octets bruts : FixedTimeEquals lève si les deux tableaux n'ont pas la même longueur, ce
        // qui romprait le temps constant recherché (l'exception fuiterait déjà "bonne longueur ou
        // pas"). Le hash a toujours 32 octets, quelle que soit la longueur de la clé fournie.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AdminKey));

        if (!CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
        {
            // 404, jamais 401/403 : ne révèle même pas l'existence de la route à qui n'a pas la clé.
            return Results.NotFound();
        }

        return await next(context);
    }
}
