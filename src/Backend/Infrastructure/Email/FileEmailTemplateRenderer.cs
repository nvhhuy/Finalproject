using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Application.Common.Email;
using Application.Contracts.Email;
using Application.Modules.Users.Authentication.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

/// <summary>Loads <c>Email/Templates/{name}.html</c> and replaces <c>[[token]]</c> placeholders.</summary>
public sealed partial class FileEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHostEnvironment _host;
    private readonly ILogger<FileEmailTemplateRenderer> _logger;
    private readonly EmailTemplateOptions _options;

    public FileEmailTemplateRenderer(
        IOptions<EmailTemplateOptions> options,
        IHostEnvironment host,
        ILogger<FileEmailTemplateRenderer> logger)
    {
        _options = options.Value;
        _host = host;
        _logger = logger;
    }

    public string Render(
        string templateName,
        IReadOnlyDictionary<string, string?> tokens,
        IReadOnlySet<string>? rawTokenNames = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name is required.", nameof(templateName));

        var html = _cache.GetOrAdd(templateName, LoadTemplate);
        var raw = rawTokenNames ?? EmailTemplateTokens.Raw;

        var result = html;
        foreach (var (key, value) in tokens)
        {
            var placeholder = $"[[{key}]]";
            if (!result.Contains(placeholder, StringComparison.Ordinal))
                continue;

            var replacement = value ?? "";
            if (!raw.Contains(key))
                replacement = WebUtility.HtmlEncode(replacement);

            result = result.Replace(placeholder, replacement, StringComparison.Ordinal);
        }

        if (TokenPlaceholder().IsMatch(result))
            _logger.LogWarning("Email template {Template} has unreplaced [[tokens]]", templateName);

        return result;
    }

    private string LoadTemplate(string templateName)
    {
        var fileName = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : $"{templateName}.html";

        var dir = _options.TemplatesDirectory.Trim();
        var candidates = new List<string>
        {
            Path.IsPathRooted(dir) ? Path.Combine(dir, fileName) : Path.Combine(_host.ContentRootPath, dir, fileName),
            Path.Combine(AppContext.BaseDirectory, dir, fileName)
        };

        foreach (var path in candidates.Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(path))
                continue;

            _logger.LogDebug("Loaded email template {Template} from {Path}", templateName, path);
            return File.ReadAllText(path);
        }

        throw new FileNotFoundException(
            $"Email template '{templateName}' not found. Tried: {string.Join(", ", candidates)}");
    }

    [GeneratedRegex(@"\[\[[a-zA-Z0-9_]+\]\]")]
    private static partial Regex TokenPlaceholder();
}
