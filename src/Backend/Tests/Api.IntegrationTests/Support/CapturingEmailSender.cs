using Application.Contracts.Email;

namespace Api.IntegrationTests.Support;

/// <summary>Records the last outbound email so integration tests can extract verification/reset tokens.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public string? LastTo { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastBody { get; private set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        LastTo = to;
        LastSubject = subject;
        LastBody = htmlBody;
        return Task.CompletedTask;
    }

    public void Clear()
    {
        LastTo = null;
        LastSubject = null;
        LastBody = null;
    }
}