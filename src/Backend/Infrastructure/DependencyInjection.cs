using Application.Contracts.Administration;
using Application.Contracts.Caching;
using Application.Contracts.Events;
using Application.Contracts.Transactions;
using Application.Contracts.Email;
using Application.Contracts.ExternalAuth;
using Application.Contracts.Media;
using Application.Contracts.Messaging;
using Application.Contracts.Storage;
using Application.Contracts.Modules.Users.Persistence;
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
using System.Text;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ILoggedInUserService, HttpContextLoggedInUserService>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddCaching(configuration);
        services.AddPersistence(configuration);
        services.AddIntegrations(configuration);
        services.AddAuth(configuration);
        services.AddScoped<IAppSettingsService, AppSettingsService>();

        return services;
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

        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3CompatibleObjectStorage>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection(SmtpEmailOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        if (configuration.GetValue($"{SmtpEmailOptions.SectionName}:Enabled", false))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, NullEmailSender>();

        services.AddSingleton<IEmailVerificationSender, EmailVerificationSender>();
        services.AddSingleton<IImageProcessingService, ImageSharpResizeService>();

        services.Configure<VideoProcessingOptions>(configuration.GetSection(VideoProcessingOptions.SectionName));
        if (!string.IsNullOrWhiteSpace(configuration["VideoProcessing:FfmpegPath"])
            && !string.IsNullOrWhiteSpace(configuration["VideoProcessing:FfprobePath"]))
        {
            services.AddSingleton<IVideoProcessingService, FfmpegCliVideoProcessingService>();
        }
        else
        {
            services.AddSingleton<IVideoProcessingService, NullVideoProcessingService>();
        }

        if (configuration.GetValue($"{RabbitMqOptions.SectionName}:Enabled", false))
        {
            services.AddSingleton<RabbitMqMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());
            services.AddSingleton<RabbitMqMessageConsumer>();
            services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<RabbitMqMessageConsumer>());
        }
        else
        {
            services.AddSingleton<IMessagePublisher, NullMessagePublisher>();
            services.AddSingleton<IMessageConsumer, NullMessageConsumer>();
        }

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        JwtSettings jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtSettings.SectionName}'.");
        JwtSettings.EnsureValidForSigning(jwtSettings);
        services.AddSingleton(Options.Create(jwtSettings));

        services.AddSingleton<IJwtGenerator, JwtGenerator>();
        services.AddScoped<IAuthenticationTokenIssuer, AuthenticationTokenIssuer>();

        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Microsoft.Extensions.Primitives.StringValues accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var unauthorizedProblem = new ProblemDetails { Status = context.Response.StatusCode, Type = "Unauthorized" };
                        return context.Response.WriteAsJsonAsync(unauthorizedProblem);
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var forbiddenProblem = new ProblemDetails { Status = context.Response.StatusCode, Type = "Forbidden" };
                        return context.Response.WriteAsJsonAsync(forbiddenProblem);
                    }
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

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(writeCs, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddDbContext<ApplicationReadDbContext>(options =>
        {
            options.UseNpgsql(readCs, npgsql => npgsql.EnableRetryOnFailure(3));
        });

        services.AddScoped<IApplicationReadDbContext>(sp => sp.GetRequiredService<ApplicationReadDbContext>());

        services.AddSingleton<IDatabaseTopology, DatabaseTopology>();

        services.AddScoped<DomainEventsSaveChangesInterceptor>();
        services.AddScoped<IAfterCommitDomainEventQueue, AfterCommitDomainEventQueue>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<AuditableInterceptor>();
        services.Configure<SeedAdminOptions>(configuration.GetSection(SeedAdminOptions.SectionName));

        services.AddScoped<IUserReadRepository, UserReadRepository>();
        services.AddScoped<IUserWriteRepository, UserWriteRepository>();
        services.AddScoped<IUserRespository, UserRepository>();
        services.AddScoped<IAdminAuditLogWriter, AdminAuditLogWriter>();
        return services;
    }

    /// <summary>Registers in-process cron scheduling (Cronos). Call from Worker host; add <see cref="Application.Contracts.Scheduling.ICronJob"/> implementations as singletons.</summary>
    public static IServiceCollection AddCronScheduling(this IServiceCollection services)
    {
        services.AddHostedService<CronJobSchedulerHostedService>();
        return services;
    }
}
