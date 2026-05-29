using Application.Modules.Users.Authentication.Queries;

namespace Application.Tests;

public sealed class LoginQueryValidatorTests
{
    private readonly LoginQueryValidator _validator = new();

    [Fact]
    public async Task Login_rejects_empty_password()
    {
        var query = new LoginQuery("a@b.com", "");

        var result = await _validator.ValidateAsync(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginQuery.Password));
    }

    [Fact]
    public async Task Login_accepts_valid_credentials_shape()
    {
        var query = new LoginQuery("a@b.com", "secret");

        var result = await _validator.ValidateAsync(query);

        Assert.True(result.IsValid);
    }
}