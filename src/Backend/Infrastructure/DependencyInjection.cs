using Application.Contracts.Administration;
using Application.Contracts.Authentication;
using Application.Contracts.Caching;
using Application.Contracts.Events;
using Application.Contracts.Transactions;
using Application.Contracts.Email;
using Application.Contracts.ExternalAuth;
using Application.Contracts.Media;
using Application.Contracts.Messaging;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Persistence;
using Application.Contracts.Storage;
using Microsoft.AspNetCore.DataProtection;
using Infrastructure.Modules.Users.Persistence.Conversions;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Settings;
using Application.Modules.Users.Authentication.Options;
using Domains.UserAggregate;
using Infrastructure.Administration;
using Infrastructure.Authentication;
using Infrastructure.Caching;
using Infrastructure.Email;
using Infrastructure.ExternalAuth;
using Infrastructure.Media;
using Infrastructure.Messaging;
using Infrastructure.Modules.Media.Persistence;
using Infrastructure.Scheduling;
using Infrastructure.Persistence.Events;
using Infrastructure.Modules.Users.Persistence.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Services;
using Infrastructure.Settings;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Core (non-HTTP) infrastructure: persistence, caching, messaging, integrations, data protection,
    /// app-settings service. Pulls no ASP.NET-specific dependencies so it is safe to call from a Worker
    /// host that doesn't have an <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </summary>
    /// <remarks>
    /// The HTTP-only pieces (JWT bearer authentication, <see cref="HttpContextLoggedInUserService"/>)
    /// live in <see cref="AddInfrastructure(IServiceCollection, IConfiguration)"/>. A Worker that
    /// only needs to run scheduled jobs or drain outbox/queues should call this method directly and
    /// register a null <see cref="ILoggedInUserService"/> (already wired below).
    /// </remarks>
    public static IServiceCollection AddInfrastructureCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ILoggedInUserService, NullLoggedInUserService>();
        services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();

        AddDataProtection(services, configuration);
        services.AddCaching(configuration);
        services.AddPersistence(configuration);
        services.AddIntegrations(configuration);
        // Bump the PBKDF2 iteration count to OWASP 2024's recommendation for PBKDF2-SHA256 (600k).
        // Identity v3 mode defaults to 100 000 which was raised from 10 000 in .NET 6 but is now
        // half-a-decade out of date; tripling it again costs ~70 ms per hash on commodity hardware,
        // negligible against the 1-2 logins/user/day workload but raises the offline crack cost
        // proportionally.
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 600_000;
        });
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAppSettingsService, AppSettingsService>();

        // Media pipeline DI lives in Application.AddApplication; here we just bind the options block.
        Application.DependencyInjection.AddMediaApplicationOptions(services, configuration);

        return services;
    }

    /// <summary>
    /// Full API infrastructure: core plus HTTP context, JWT bearer authentication, and the principal
    /// resolver. Use this from the <c>Api</c> host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructureCore(configuration);

        services.AddHttpContextAccessor();
        // Replace the worker-safe null implementation with the HTTP-aware one in API hosts.
        services.AddScoped<ILoggedInUserService, HttpContextLoggedInUserService>();
        services.AddAuth(configuration);

        return services;
    }

    private static void AddDataProtection(IServiceCollection services, IConfiguration configuration)
    {
        // The DataProtection key ring encrypts every column marked with the EncryptedStringConverter
        // (TOTP secrets, etc.) plus auth cookies and antiforgery tokens. If the ring is ephemeral
        // (in-memory only), every API restart invalidates those columns — users lose access to
        // 2FA codes and have to re-enrol. So:
        //   - dev:  in-memory is acceptable (developers expect resets).
        //   - prod: KeysDirectory MUST be set so keys outlive container restarts and are shared
        //           between API replicas behind a load balancer.
        //
        // We fail fast when the host looks production-like AND no directory is configured. The
        // env-var lookup is intentional: ASPNETCORE_ENVIRONMENT is what ASP.NET reads to set
        // IHostEnvironment.EnvironmentName, but we're called before that resolution.
        IDataProtectionBuilder builder = services.AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "finalproject");

        string? keysDirectory = configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysDirectory))
        {
            Directory.CreateDirectory(keysDirectory);
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
            return;
        }

        bool isDevelopment = IsDevelopmentHost(configuration);
        if (!isDevelopment && !configuration.GetValue("DataProtection:AllowEphemeralKeys", false))
        {
            // Fail at startup rather than at first 2FA setup hours later.
            throw new InvalidOperationException(
                "DataProtection:KeysDirectory is not configured. " +
                "In-memory keys reset on restart, which invalidates every encrypted column and " +
                "logs every user out across replicas. Set DataProtection:KeysDirectory to a " +
                "shared volume, or set DataProtection:AllowEphemeralKeys=true to acknowledge " +
                "the risk (single-instance, ephemeral data only).");
        }
    }

    /// <summary>
    /// Resolves whether the current host is running in the Development environment by inspecting
    /// every place ASP.NET Core / generic host might surface the value. We need this because
    /// service registration runs before <see cref="Microsoft.Extensions.Hosting.IHostEnvironment"/>
    /// is wired up via DI, and different launch paths populate the value through different keys:
    /// <list type="bullet">
    ///   <item><description><c>HostDefaults.EnvironmentKey</c> — what <c>WebApplicationBuilder</c> and <c>WebApplicationFactory.UseEnvironment</c> set.</description></item>
    ///   <item><description><c>ASPNETCORE_ENVIRONMENT</c> / <c>DOTNET_ENVIRONMENT</c> — the conventional env vars exposed through the default configuration sources.</description></item>
    ///   <item><description>OS-level env vars — used as a final fallback so Worker / CLI hosts that bypass the default builder still get a correct answer.</description></item>
    /// </list>
    /// Falling back to "Production" biases misconfiguration toward the safer failure mode (e.g.
    /// the JWT dev-sentinel guard and the ephemeral-key guard both refuse to start there).
    /// </summary>
    private static bool IsDevelopmentHost(IConfiguration configuration)
    {
        string env = configuration[Microsoft.Extensions.Hosting.HostDefaults.EnvironmentKey]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
        return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        string? redis = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }

    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PasswordResetEmailOptions>(configuration.GetSection(PasswordResetEmailOptions.SectionName));
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection(SmtpEmailOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        if (configuration.GetValue($"{SmtpEmailOptions.SectionName}:Enabled", false))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, NullEmailSender>();

        services.AddSingleton<IEmailVerificationSender, EmailVerificationSender>();

        // Object storage (R2 / S3 / MinIO compatible) — singleton because the AWS SDK client is thread-safe.
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3CompatibleObjectStorage>();

        // Media processing services. Image is always available; video falls back to a loud null
        // implementation when FFmpeg is not installed on the host.
        services.AddSingleton<IImageProcessingService, ImageSharpProcessingService>();
        services.Configure<VideoProcessingOptions>(configuration.GetSection(VideoProcessingOptions.SectionName));
        if (!string.IsNullOrWhiteSpace(configuration[$"{VideoProcessingOptions.SectionName}:FfmpegPath"])
            && !string.IsNullOrWhiteSpace(configuration[$"{VideoProcessingOptions.SectionName}:FfprobePath"]))
        {
            services.AddSingleton<IVideoProcessingService, FfmpegVideoProcessingService>();
        }
        else
        {
            services.AddSingleton<IVideoProcessingService, NullVideoProcessingService>();
        }

        // Malware / content scanner. Default to the no-op implementation; operators enable the
        // ClamAV-backed scanner via configuration (MediaScanner:Enabled=true + Host/Port).
        services.Configure<MediaScannerOptions>(configuration.GetSection(MediaScannerOptions.SectionName));
        if (configuration.GetValue($"{MediaScannerOptions.SectionName}:Enabled", false))
            services.AddSingleton<IMediaScanner, ClamdMediaScanner>();
        else
            services.AddSingleton<IMediaScanner, NullMediaScanner>();

        if (configuration.GetValue($"{RabbitMqOptions.SectionName}:Enabled", false))
        {
            services.AddSingleton<RabbitMqMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());
            services.AddSingleton<RabbitMqMessageConsumer>();
            services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<RabbitMqMessageConsumer>());
            services.AddSingleton<IDeadLetterAdminService, RabbitMqDeadLetterAdminService>();
        }
        else
        {
            services.AddSingleton<IMessagePublisher, NullMessagePublisher>();
            services.AddSingleton<IMessageConsumer, NullMessageConsumer>();
            services.AddSingleton<IDeadLetterAdminService, NullDeadLetterAdminService>();
        }

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        JwtSettings jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtSettings.SectionName}'.");

        bool isDevelopment = IsDevelopmentHost(configuration);
        JwtSettings.EnsureValidForSigning(jwtSettings, isDevelopment);
        services.AddSingleton(Options.Create(jwtSettings));

        services.AddSingleton<IJwtGenerator, JwtGenerator>();
        services.AddScoped<IAuthenticationTokenIssuer, AuthenticationTokenIssuer>();
        services.Configure<AccountLockoutOptions>(configuration.GetSection(AccountLockoutOptions.SectionName));
        services.AddScoped<IFailedLoginTracker, CacheFailedLoginTracker>();

        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Token validation policy. Every flag set explicitly (rather than relying on
                // default-true) so a future hand-edit can't silently flip one off.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };

                // Only the SignalR hub-token handshake lives here. The 401 / 403 envelope shaping
                // is delegated to `AddJwtEnvelopeErrorResponses` (PostConfigure) so there is a
                // single source of truth for the response shape; setting OnChallenge here too
                // would silently lose to the later PostConfigure overwrite.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // WebSockets can't carry custom headers, so SignalR uses ?access_token=.
                        // We honour it only for the /hubs path so other endpoints can't smuggle a
                        // token through the query string and have it logged by upstream proxies.
                        Microsoft.Extensions.Primitives.StringValues accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        string? writeCs = configuration.GetConnectionString("DatabaseConnectionString");
        string? readCs = configuration.GetConnectionString("DatabaseReadConnectionString");
        if (string.IsNullOrWhiteSpace(readCs))
            readCs = writeCs;

        // Retry policy: 3 attempts is enough to cross a Postgres failover / network blip without
        // pinning a request thread for too long. `maxRetryDelay = 5s` caps the exponential backoff
        // so a sustained outage trips the request quickly instead of holding the connection pool.
        // The errorCodesToAdd list is empty — we rely on the Npgsql provider's curated default
        // set of transient codes (deadlocks, serialization failures, connection drops).
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            UserDataProtection.Initialize(sp.GetRequiredService<IDataProtectionProvider>());
            options.UseNpgsql(writeCs, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null));
        });

        services.AddDbContext<ApplicationReadDbContext>((sp, options) =>
        {
            UserDataProtection.Initialize(sp.GetRequiredService<IDataProtectionProvider>());
            options.UseNpgsql(readCs, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null));
        });

        services.AddScoped<IApplicationReadDbContext>(sp => sp.GetRequiredService<ApplicationReadDbContext>());

        services.AddScoped<DomainEventsSaveChangesInterceptor>();
        services.AddScoped<IAfterCommitDomainEventQueue, AfterCommitDomainEventQueue>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));

        services.AddScoped<IUserReadRepository, UserReadRepository>();
        services.AddScoped<IUserWriteRepository, UserWriteRepository>();
        services.AddScoped<IAdminAuditLogWriter, AdminAuditLogWriter>();
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        return services;
    }

    /// <summary>Registers in-process cron scheduling (Cronos). Call from Worker host; add <see cref="Application.Contracts.Scheduling.ICronJob"/> implementations as singletons.</summary>
    public static IServiceCollection AddCronScheduling(this IServiceCollection services)
    {
        services.AddHostedService<CronJobSchedulerHostedService>();
        return services;
    }
}
