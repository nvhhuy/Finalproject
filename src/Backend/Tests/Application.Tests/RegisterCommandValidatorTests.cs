using Application.Modules.Users.Authentication.Commands.Register;

namespace Application.Tests;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public async Task Register_rejects_empty_email()
    {
        var command = new RegisterCommand("A", "B", "u1", "", "Password12");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Email));
    }

    [Fact]
    public async Task Register_rejects_invalid_email()
    {
        var command = new RegisterCommand("A", "B", "u1", "not-an-email", "Password12");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Register_accepts_valid_payload()
    {
        var command = new RegisterCommand("A", "B", "u1", "u1@example.com", "Password12");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Register_rejects_password_without_uppercase()
    {
        var command = new RegisterCommand("A", "B", "u1", "u1@example.com", "alllowercase1");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Password));
    }

    [Fact]
    public async Task Register_rejects_password_too_short()
    {
        var command = new RegisterCommand("A", "B", "u1", "u1@example.com", "Short1");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCommand.Password));
    }
}
