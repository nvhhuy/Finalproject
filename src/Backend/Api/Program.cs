using Api;
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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(PlatformSerilog.Configure("api"));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddWebApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPlatformOpenTelemetry(builder.Configuration, serviceNameFallback: "finalproject.api");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
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
            await DbSeeder.SeedAdminIfMissingAsync(db, scope.ServiceProvider);
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

app.UseHttpsRedirection();

app.UseCorrelationId();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out object? cid) && cid is string correlationId)
            diagnosticContext.Set("CorrelationId", correlationId);
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserName", httpContext.User.Identity?.Name ?? "(anonymous)");
    };
});

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRealtimeHubs();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
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
