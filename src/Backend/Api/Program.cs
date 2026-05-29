using Api;
using Api.Authentication;
using Api.ExceptionHandling;
using Api.Middleware;
using Application;
using Asp.Versioning.ApiExplorer;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Observability;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(PlatformSerilog.Configure("api"));

// Kestrel: drop the `Server: Kestrel` response header so CVE scanners cannot fingerprint the
// runtime; combined with HSTS + CSP this is part of the baseline web-app hardening posture.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// HSTS production tuning. Defaults match the OWASP / Mozilla guidance:
//   max-age = 2 years, includeSubDomains, preload (eligible for https://hstspreload.org).
// Operators can override each knob via the `Hsts` config section if a different deployment
// posture is required. Disabled in Development by Program.cs (`app.UseHsts()` only runs outside
// Development) — these options take effect when it does run.
int hstsMaxAgeDays = builder.Configuration.GetValue("Hsts:MaxAgeDays", 730);
bool hstsIncludeSubDomains = builder.Configuration.GetValue("Hsts:IncludeSubDomains", true);
bool hstsPreload = builder.Configuration.GetValue("Hsts:Preload", true);
builder.Services.AddHsts(options =>
{
    options.Preload = hstsPreload;
    options.IncludeSubDomains = hstsIncludeSubDomains;
    options.MaxAge = TimeSpan.FromDays(hstsMaxAgeDays);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust the proxies the operator declares in config. Without this the rate limiter and
    // refresh-token IP audit log see only the LB / ingress IP for every request (which means
    // either rate limits hit the wrong partition, or one client can DOS the whole fleet).
    // Each entry can be a single IP ("10.0.0.5") or a CIDR network ("10.0.0.0/8").
    foreach (string entry in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(entry, out IPAddress? proxyIp))
            options.KnownProxies.Add(proxyIp);
    }
    foreach (string entry in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        string[] parts = entry.Split('/', 2);
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out IPAddress? prefix)
            && int.TryParse(parts[1], out int prefixLength))
        {
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    }

    // Limit the number of forwarded headers we honour to defend against header-stuffing tricks
    // (a malicious upstream that appends `X-Forwarded-For: <victim>` to every request).
    options.ForwardLimit = builder.Configuration.GetValue("ReverseProxy:ForwardLimit", 2);
});

builder.Services.AddWebApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Must run AFTER AddInfrastructure registers the JwtBearer scheme: PostConfigure unifies the
// 401/403 response shape with the ApiResponse<T> envelope used by every other endpoint.
builder.Services.AddJwtEnvelopeErrorResponses();
builder.Services.AddPlatformOpenTelemetry(builder.Configuration, serviceNameFallback: "baseproject.api");

var app = builder.Build();

// Production host-header guard: ASP.NET Core's HostFiltering middleware honours `AllowedHosts`
// but defaults to "*" which means any Host header is accepted. That's fine in dev but in
// production it lets attackers forge absolute URLs for password-reset / email-verification
// flows that read `Request.Host`. We refuse to boot if a non-dev environment still uses the
// wildcard so misconfiguration is loud rather than silent.
if (!app.Environment.IsDevelopment())
{
    string allowedHosts = app.Configuration.GetValue("AllowedHosts", "*") ?? "*";
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
    {
        throw new InvalidOperationException(
            "AllowedHosts must be set to the deployment's public hostname(s) outside Development " +
            "(e.g. \"api.example.com;www.example.com\"). Refusing to start with the wildcard value.");
    }
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

// Swagger UI + JSON: always on in Development, opt-in via Swagger:ExposeInProduction in other
// environments. Production deployments that want it locked down keep the config flag false and
// rely on a build-time OpenAPI artefact shipped separately. When it IS exposed in production,
// the deployment should reverse-proxy /swagger behind whatever ingress auth your gateway uses
// (we don't gate per-user here because Swagger UI is anonymous-friendly tooling).
bool exposeSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Swagger:ExposeInProduction", false);
if (exposeSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        options.DocumentTitle = "API";
    });
}

bool applyPendingMigrations = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Database:ApplyPendingMigrations", false);
bool seedBootstrapAdmin = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Seed:Admin:Enabled", false);

if (applyPendingMigrations || seedBootstrapAdmin)
{
    using IServiceScope scope = app.Services.CreateScope();
    Microsoft.Extensions.Logging.ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (applyPendingMigrations)
            await db.Database.MigrateAsync();

        if (seedBootstrapAdmin)
        {
            await DbSeeder.SeedAdminIfMissingAsync(db, scope.ServiceProvider);
            await DbSeeder.SeedDefaultTenantIfMissingAsync(db, scope.ServiceProvider);
            await DbSeeder.SeedDefaultPlansIfMissingAsync(db, scope.ServiceProvider);
            await DbSeeder.SeedDefaultCreditPackagesIfMissingAsync(db, scope.ServiceProvider);
        }
    }
    catch (Exception ex) when (ex.Message.Contains("Failed to connect", StringComparison.Ordinal))
    {
        logger.LogCritical(
            ex,
            "Database server is not reachable. Start PostgreSQL (connection string in appsettings), for example from the repo root: docker compose -f local/docker-compose.yml up -d postgres");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseExceptionHandler();
}

app.UseForwardedHeaders();

// In Development the http profile listens on :5000; redirecting every call to https breaks
// browser CORS preflight (307 responses carry no Access-Control-Allow-Origin).
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCorrelationId();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out object? cid) && cid is string correlationId)
            diagnosticContext.Set("CorrelationId", correlationId);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);

        // We log the user id (sub claim) instead of `Identity.Name` so the structured log lines
        // never carry email / username PII. Anonymous and unauthenticated paths show "anonymous".
        string sub = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? "anonymous";
        diagnosticContext.Set("UserSub", sub);

        // SignalR's WebSocket handshake passes the JWT via `?access_token=...` because the
        // browser API doesn't allow custom headers on the upgrade request. Without this filter
        // the raw token ends up in every request log line for /hubs/* and any downstream log
        // sink (Loki, Splunk, Datadog) becomes a credential database. Strip the query string for
        // hub paths before structured logging captures it.
        if (httpContext.Request.Path.StartsWithSegments("/hubs"))
            diagnosticContext.Set("RequestQuery", "<redacted>");
        else if (httpContext.Request.QueryString.HasValue)
            diagnosticContext.Set("RequestQuery", httpContext.Request.QueryString.Value);
    };
});

// UseCors must run AFTER UseRouting and BEFORE UseAuthentication. The rate limiter sits
// after CORS so a CORS preflight that the browser sends every 10 minutes doesn't burn
// a token bucket entry on its own.
app.UseRouting();
app.UseCors(Api.Configuration.CorsOptions.PolicyName);

app.UseAuthentication();
app.UseMiddleware<Api.Middleware.TenantContextMiddleware>();

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-site");
    context.Response.Headers.Append(
        "Permissions-Policy",
        "accelerometer=(), ambient-light-sensor=(), autoplay=(), battery=(), camera=(), " +
        "clipboard-read=(), clipboard-write=(), display-capture=(), document-domain=(), " +
        "encrypted-media=(), fullscreen=(), geolocation=(), gyroscope=(), interest-cohort=(), " +
        "magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), " +
        "publickey-credentials-get=(), screen-wake-lock=(), sync-xhr=(), usb=(), web-share=(), " +
        "xr-spatial-tracking=()");

    // CSP: the API serves JSON to the SPA + the Swagger UI bundle (when enabled). We deliberately
    // skip CSP on /swagger to avoid breaking Swagger UI's inline scripts / styles — for every
    // other path we restrict to self + the configured asset origins. Tighten further once an SPA
    // origin policy is finalised.
    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    }
    await next();
});

app.UseAuthorization();

app.MapControllers().RequireRateLimiting("tenant-api");
app.MapRealtimeHubs();

// Health endpoints are split so the liveness probe stays cheap and information-free, while the
// readiness probe — which actually pings every dependency — only answers callers inside the
// trust boundary. Operators that want to publish `/health/ready` externally should put it
// behind an ingress auth filter; the codebase keeps it open to any caller that can reach the
// pod since most platforms (k8s, ECS, Fly) probe it from inside the cluster.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
});

// Back-compat alias so existing probes pointed at /health keep working through a deploy. New
// deployments should hit /health/live (anonymous) or /health/ready (in-cluster).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
});

app.Run();

public partial class Program;

/// <summary>Small JSON for load balancers / k8s without pulling extra packages.</summary>
internal static class HealthCheckResponseWriter
{
    public static Task WriteMinimalJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        string status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";
        return context.Response.WriteAsync($"{{\"status\":\"{status}\"}}");
    }
}
