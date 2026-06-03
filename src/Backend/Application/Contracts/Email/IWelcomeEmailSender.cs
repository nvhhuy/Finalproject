namespace Application.Contracts.Email;

public interface IWelcomeEmailSender
{
    Task SendWelcomeEmailAsync(string email, string? displayName, CancellationToken cancellationToken = default);
}
