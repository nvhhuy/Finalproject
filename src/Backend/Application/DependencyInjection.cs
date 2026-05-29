using Application.Common.Behaviors;
using Application.Contracts.Media;
using Application.Modules.Media;
using Application.Modules.Media.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Pipeline order is outer-to-inner: Logging wraps Validation wraps Authorization wraps the handler.
        // ValidationBehavior is intentionally above AdminAuthorizationBehavior so that obviously malformed
        // requests fail fast with 400 before we report 403, matching standard ASP.NET filter ordering.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AdminAuthorizationBehavior<,>));

        // Media pipeline (shared by the API host and the worker host).
        services.AddScoped<IMediaProcessingPipeline, MediaProcessingPipeline>();

        return services;
    }

    /// <summary>
    ///     Binds <see cref="MediaUploadOptions" /> from the configuration root. Host packages call this
    ///     in addition to <see cref="AddApplication" /> so the binding can read their <c>IConfiguration</c>.
    /// </summary>
    public static IServiceCollection AddMediaApplicationOptions(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MediaUploadOptions>(configuration.GetSection(MediaUploadOptions.SectionName));
        return services;
    }
}