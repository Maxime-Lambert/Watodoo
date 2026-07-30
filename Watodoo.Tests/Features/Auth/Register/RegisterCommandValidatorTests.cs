using Watodoo.Features.Auth.Register;

namespace Watodoo.Tests.Features.Auth.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Theory]
    [InlineData("", "Passw0rd")]
    [InlineData("not-an-email", "Passw0rd")]
    public void Invalid_email_fails_validation(string email, string password)
    {
        var result = _validator.Validate(new RegisterCommand(email, password));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1A")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    public void Invalid_password_fails_validation(string password)
    {
        var result = _validator.Validate(new RegisterCommand("user@example.com", password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_email_and_password_pass_validation()
    {
        var result = _validator.Validate(new RegisterCommand("user@example.com", "Passw0rd1"));

        Assert.True(result.IsValid);
    }
}
