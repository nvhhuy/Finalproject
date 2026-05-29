using Application.Contracts.Email;

namespace Infrastructure.Email;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}