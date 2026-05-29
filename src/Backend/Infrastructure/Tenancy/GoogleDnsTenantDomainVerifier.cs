using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Contracts.Tenancy;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Tenancy;

/// <summary>DNS TXT verification via Google Public DNS JSON API (no extra NuGet).</summary>
public sealed class GoogleDnsTenantDomainVerifier : ITenantDomainDnsVerifier
{
    private readonly HttpClient _http;
    private readonly TenancyHostOptions _options;
    private readonly ILogger<GoogleDnsTenantDomainVerifier> _logger;

    public GoogleDnsTenantDomainVerifier(
        HttpClient http,
        Microsoft.Extensions.Options.IOptions<TenancyHostOptions> options,
        ILogger<GoogleDnsTenantDomainVerifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> HasVerificationTxtRecordAsync(
        string hostname,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        string prefix = _options.VerificationTxtPrefix.Trim('.');
        string recordName = $"{prefix}.{hostname}";
        try
        {
            string url =
                $"https://dns.google/resolve?name={Uri.EscapeDataString(recordName)}&type=TXT";
            DnsGoogleResponse? payload = await _http.GetFromJsonAsync<DnsGoogleResponse>(url, cancellationToken);
            if (payload?.Answer is null)
                return false;

            foreach (DnsGoogleAnswer answer in payload.Answer)
            {
                if (answer.Type != 16 || string.IsNullOrWhiteSpace(answer.Data))
                    continue;

                string data = answer.Data.Trim('"');
                if (data.Contains(verificationToken, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "DNS TXT lookup failed for {RecordName}", recordName);
            return false;
        }
    }

    private sealed class DnsGoogleResponse
    {
        [JsonPropertyName("Answer")]
        public DnsGoogleAnswer[]? Answer { get; init; }
    }

    private sealed class DnsGoogleAnswer
    {
        [JsonPropertyName("type")]
        public int Type { get; init; }

        [JsonPropertyName("data")]
        public string? Data { get; init; }
    }
}
