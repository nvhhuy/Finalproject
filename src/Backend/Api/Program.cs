using Api;
using Api.ExceptionHandling;
using Api.Middleware;
using Application;
using Domains.UserAggregate;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    using (var scope = app.Services.CreateScope())
    {
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, scope.ServiceProvider);
    }
}
else
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
