using System.Net.Http.Json;
using Application.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
///     Verifies Cloudflare Turnstile challenge tokens via the siteverify REST API.
///     When <see cref="TurnstileOptions.Enabled" /> is <c>false</c> all tokens are accepted
///     so development and test environments are not blocked.
/// </summary>
public sealed class TurnstileService : ITurnstileService
{
    private readonly HttpClient _http;
    private readonly TurnstileOptions _options;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(HttpClient http, IOptions<TurnstileOptions> options, ILogger<TurnstileService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(string? token, string? remoteIp = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Turnstile validation failed: token is missing");
            return false;
        }

        try
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new("secret", _options.SecretKey),
                new("response", token)
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
                formData.Add(new KeyValuePair<string, string>("remoteip", remoteIp));

            using var content = new FormUrlEncodedContent(formData);
            using var response = await _http.PostAsync(_options.VerifyUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile siteverify returned HTTP {Status}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileSiteverifyResponse>(
                cancellationToken: cancellationToken);

            if (result is null)
            {
                _logger.LogWarning("Turnstile siteverify returned an empty or unparseable body");
                return false;
            }

            if (!result.Success)
                _logger.LogWarning("Turnstile validation failed with error codes: {Codes}",
                    string.Join(", ", result.ErrorCodes ?? []));

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile siteverify call threw an exception");
            return false;
        }
    }

    private sealed class TurnstileSiteverifyResponse
    {
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
