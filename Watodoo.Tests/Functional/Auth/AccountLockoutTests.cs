using System.Net;
using System.Net.Http.Json;
using Watodoo.Features.Auth.Login;
using Watodoo.Features.Auth.Register;

namespace Watodoo.Tests.Functional.Auth;

[Collection(nameof(FunctionalTestCollection))]
public sealed class AccountLockoutTests(FunctionalTestFixture fixture)
{
    // Valeur par défaut de Lockout:MaxFailedAccessAttempts (Program.cs), non surchargée dans
    // FunctionalTestFixture : le lockout est scopé par utilisateur (emails uniques par test),
    // donc pas besoin d'un seuil bas dédié comme pour le rate limiter/le job de cleanup, qui sont
    // globaux. Si le défaut change dans Program.cs, ce test doit être mis à jour en conséquence.
    private const int MaxFailedAccessAttempts = 5;
    private const string CorrectPassword = "Passw0rd1";
    private const string WrongPassword = "WrongPassword1";

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

    private HttpClient CreateClient() => fixture.Factory.CreateClient();

    private async Task<string> RegisterUser(HttpClient client)
    {
        var email = UniqueEmail();
        var response = await client.PostAsJsonAsync("/auth/register", new RegisterCommand(email, CorrectPassword));
        // Sans ça, un échec silencieux de l'inscription ferait passer le test au vert pour la
        // mauvaise raison (401 "utilisateur inexistant" partout, jamais un vrai verrouillage).
        response.EnsureSuccessStatusCode();
        return email;
    }

    private static Task<HttpResponseMessage> LoginAttempt(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginCommand(email, password));

    [Fact]
    public async Task Locks_out_after_reaching_the_failed_attempts_threshold_even_with_the_correct_password()
    {
        var client = CreateClient();
        var email = await RegisterUser(client);

        for (var i = 0; i < MaxFailedAccessAttempts; i++)
        {
            var failedAttempt = await LoginAttempt(client, email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, failedAttempt.StatusCode);
        }

        var lockedOutAttempt = await LoginAttempt(client, email, CorrectPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, lockedOutAttempt.StatusCode);
    }

    [Fact]
    public async Task Does_not_lock_out_before_reaching_the_threshold()
    {
        var client = CreateClient();
        var email = await RegisterUser(client);

        for (var i = 0; i < MaxFailedAccessAttempts - 1; i++)
        {
            await LoginAttempt(client, email, WrongPassword);
        }

        var response = await LoginAttempt(client, email, CorrectPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_the_failed_attempts_counter()
    {
        var client = CreateClient();
        var email = await RegisterUser(client);

        for (var i = 0; i < MaxFailedAccessAttempts - 1; i++)
        {
            await LoginAttempt(client, email, WrongPassword);
        }

        var successfulLogin = await LoginAttempt(client, email, CorrectPassword);
        Assert.Equal(HttpStatusCode.OK, successfulLogin.StatusCode);

        for (var i = 0; i < MaxFailedAccessAttempts - 1; i++)
        {
            await LoginAttempt(client, email, WrongPassword);
        }

        var response = await LoginAttempt(client, email, CorrectPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
