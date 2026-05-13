using Api.Authorization;
using Api.Configuration;
using Api.ExceptionHandling;
using Api.Filters;
using Api.Hubs;
using Domains.UserAggregate;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Api;

/// <summary>Registers API-only services so <see cref="Program"/> stays a thin host composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddControllers();
        services.AddScoped<TransactionalActionFilter>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(UserRoles.Admin));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    }));
        });

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database",
                tags: new[] { "db", "ready" });

        AddRealtimeServices(services, configuration);

        return services;
    }

    private static void AddRealtimeServices(IServiceCollection services, IConfiguration configuration)
    {
        RealtimeOptions? opts = configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>();
        if (opts?.Enabled != true)
            return;

        ISignalRServerBuilder signalR = services.AddSignalR();
        if (opts.UseRedisBackplane && !string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]))
            signalR.AddStackExchangeRedis(configuration["Redis:ConnectionString"]!);
    }

    /// <summary>Maps SignalR hubs when <see cref="RealtimeOptions.Enabled"/> is true.</summary>
    public static WebApplication MapRealtimeHubs(this WebApplication app)
    {
        RealtimeOptions? opts = app.Configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>();
        if (opts?.Enabled == true)
            app.MapHub<NotificationHub>("/hubs/notifications");
        return app;
    }
}
