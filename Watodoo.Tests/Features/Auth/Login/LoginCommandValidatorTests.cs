using Watodoo.Features.Auth.Login;

namespace Watodoo.Tests.Features.Auth.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("user@example.com", "")]
    public void Invalid_command_fails_validation(string email, string password)
    {
        var result = _validator.Validate(new LoginCommand(email, password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes_validation()
    {
        var result = _validator.Validate(new LoginCommand("user@example.com", "anything"));

        Assert.True(result.IsValid);
    }
}
