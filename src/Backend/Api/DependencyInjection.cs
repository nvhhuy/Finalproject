using System.Security.Claims;
using System.Threading.RateLimiting;
using Api.Authorization;
using Api.Configuration;
using Api.ExceptionHandling;
using Api.Filters;
using Api.Hubs;
using Api.Realtime;
using Application.Contracts.Billing;
using Application.Contracts.Notifications;
using Application.Contracts.Tenancy;
using Asp.Versioning;
using Domains.UserAggregate;
using Infrastructure.HealthChecks;
using Infrastructure.Modules.Billing;
using Infrastructure.Modules.Notifications;
using Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api;

/// <summary>Registers API-only services so <see cref="Program" /> stays a thin host composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddCorsPolicy(configuration);
        services.AddScoped<TransactionalActionFilter>();
        services.AddScoped<IdempotencyActionFilter>();
        services.AddControllers(options => { options.Filters.AddService<IdempotencyActionFilter>(); });
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

        services.AddMemoryCache();
        services.Configure<TenantRateLimitOptions>(configuration.GetSection(TenantRateLimitOptions.SectionName));
        services.AddScoped<ITenantRateLimitProvider, TenantRateLimitProvider>();

        var disableRateLimiting = configuration.GetValue<bool>("IntegrationTesting:DisableRateLimiting");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { success = false, message = "Tenant API rate limit exceeded. Try again shortly." },
                    token);
            };

            options.AddPolicy("tenant-api", httpContext =>
            {
                if (disableRateLimiting || IsTenantRateLimitExempt(httpContext))
                    return RateLimitPartition.GetNoLimiter("exempt");

                var provider =
                    httpContext.RequestServices.GetRequiredService<ITenantRateLimitProvider>();
                var tenantContext =
                    httpContext.RequestServices.GetRequiredService<ITenantContext>();

                var tenantId = tenantContext.TenantId;
                var permitLimit = provider.GetRequestsPerMinute(tenantId);
                var partitionKey = tenantId is { } id && id != Guid.Empty
                    ? $"tenant:{id:N}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy("auth", context =>
            {
                if (disableRateLimiting)
                    return RateLimitPartition.GetNoLimiter("exempt");

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });

            // Per-user limit for media uploads. Keyed by the authenticated user id (falling back
            // to IP for any unauthenticated path that slips through). Tight enough to stop a
            // single token from spamming the bucket (60 inits/min ≈ 1/s sustained) without
            // friction for normal social-media usage.
            options.AddPolicy("media-upload", context =>
            {
                if (disableRateLimiting)
                    return RateLimitPartition.GetNoLimiter("exempt");

                var partitionKey = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                   ?? context.Connection.RemoteIpAddress?.ToString()
                                   ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                "database",
                tags: new[] { "db", "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "ready" })
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "messaging", "ready" })
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: new[] { "storage", "ready" })
            .AddCheck<SmtpHealthCheck>("smtp", tags: new[] { "email" });

        AddRealtimeServices(services, configuration);

        return services;
    }

    private static bool IsTenantRateLimitExempt(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments("/health")
               || path.StartsWithSegments("/swagger")
               || path.StartsWithSegments("/hubs")
               || path.Value?.Contains("/webhooks/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var opts = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        services.AddCors(o => o.AddPolicy(CorsOptions.PolicyName, builder =>
        {
            // Empty origin list = browser CORS not needed (backend-to-backend only). We still
            // register an empty policy so UseCors can run without conditionals in Program.cs.
            if (opts.AllowedOrigins.Length > 0)
                builder.WithOrigins(opts.AllowedOrigins);

            // Explicit allowlists instead of AllowAnyHeader/AllowAnyMethod: a header-injection
            // bug in the SPA can't relay arbitrary headers cross-origin, and a future server-side
            // method handler can't be invoked via CORS unless the operator opts the method in.
            builder
                .WithHeaders(opts.AllowedHeaders)
                .WithMethods(opts.AllowedMethods)
                .WithExposedHeaders(opts.ExposedHeaders)
                .SetPreflightMaxAge(TimeSpan.FromSeconds(opts.PreflightMaxAgeSeconds));

            // Credentialed CORS is incompatible with AllowAnyOrigin per the spec; the
            // WithOrigins call above scopes it correctly.
            if (opts.AllowCredentials && opts.AllowedOrigins.Length > 0)
                builder.AllowCredentials();
        }));
        return services;
    }

    private static void AddRealtimeServices(IServiceCollection services, IConfiguration configuration)
    {
        var opts = configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>();
        if (opts?.Enabled == true)
        {
            var signalR = services.AddSignalR(hub =>
            {
                hub.MaximumReceiveMessageSize = 32 * 1024;
                hub.EnableDetailedErrors = false;
                hub.KeepAliveInterval = TimeSpan.FromSeconds(15);
                hub.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
            });
            if (opts.UseRedisBackplane && !string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]))
                signalR.AddStackExchangeRedis(configuration["Redis:ConnectionString"]!);

            services.AddSingleton<INotificationDispatcher, SignalRNotificationDispatcher>();
        }
        else
        {
            services.AddSingleton<INotificationDispatcher, NullNotificationDispatcher>();
        }
    }

    /// <summary>Maps SignalR hubs when <see cref="RealtimeOptions.Enabled" /> is true.</summary>
    public static WebApplication MapRealtimeHubs(this WebApplication app)
    {
        var opts = app.Configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>();
        if (opts?.Enabled == true)
            app.MapHub<NotificationHub>("/hubs/notifications");
        return app;
    }
}