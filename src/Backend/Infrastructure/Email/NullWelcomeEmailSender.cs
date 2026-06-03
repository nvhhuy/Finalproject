using Application.Contracts.Email;

namespace Infrastructure.Email;

public sealed class NullWelcomeEmailSender : IWelcomeEmailSender
{
    public Task SendWelcomeEmailAsync(string email, string? displayName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
