using Api.Authorization;
using Api.Configuration;
using Api.ExceptionHandling;
using Api.Filters;
using Api.Hubs;
using Asp.Versioning;
using Domains.UserAggregate;
using Infrastructure.HealthChecks;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
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
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("x-api-version"));
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

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
                tags: new[] { "db", "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "ready" })
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "messaging", "ready" })
            .AddCheck<SmtpHealthCheck>("smtp", tags: new[] { "email" });

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
