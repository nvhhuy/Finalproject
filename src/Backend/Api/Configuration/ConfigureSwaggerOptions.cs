using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Configuration;

/// <summary>Adds one Swagger document per discovered API version (e.g. v1, v2).</summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        options.CustomSchemaIds(type => type.FullName?.Replace("+", ".", StringComparison.Ordinal));

        foreach (var description in _provider.ApiVersionDescriptions)
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = $"API {description.GroupName.ToUpperInvariant()}",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated ? "This API version has been deprecated." : null
            });

        // Bearer security definition + global requirement so Swagger UI surfaces an Authorize
        // button. Without these, the UI's "Try it out" cannot call any [Authorize] endpoint.
        // We register a per-operation requirement instead of a hard global so that the few
        // anonymous endpoints (Register / Login / ForgotPassword) don't show a misleading lock
        // icon on the Swagger UI page.
        var bearerScheme = new OpenApiSecurityScheme
        {
            Description = "JWT bearer token. Format: 'Bearer {token}'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
        };
        options.AddSecurityDefinition("Bearer", bearerScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [bearerScheme] = Array.Empty<string>()
        });
    }
}