using Application.Contracts.Email;

namespace Api.IntegrationTests.Support;

/// <summary>Records the last outbound email so integration tests can extract verification/reset tokens.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Subject, string Body)> _messages = [];

    public string? LastTo => _messages.Count > 0 ? _messages[^1].To : null;
    public string? LastSubject => _messages.Count > 0 ? _messages[^1].Subject : null;
    public string? LastBody => _messages.Count > 0 ? _messages[^1].Body : null;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _messages.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public (string To, string Subject, string Body)? FindLast(Func<string, bool> subjectPredicate)
    {
        for (var i = _messages.Count - 1; i >= 0; i--)
        {
            if (subjectPredicate(_messages[i].Subject))
                return _messages[i];
        }

        return null;
    }

    public void Clear() => _messages.Clear();
}