namespace Application.Contracts.Email;

/// <summary>Sends a time-limited email verification link (cache-backed token).</summary>
public interface IEmailVerificationSender
{
    Task SendVerificationEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);
}