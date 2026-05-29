using Application.Contracts.Authorization;
using Application.Contracts.Settings;

namespace Application.Modules.SystemSettings.Admin.Queries;

public sealed record GetStringAppSettingQuery(string Key, string DefaultValue)
    : IRequest<Result<string, Error>>, IRequireAdmin;

public sealed class GetStringAppSettingQueryHandler : IRequestHandler<GetStringAppSettingQuery, Result<string, Error>>
{
    private readonly IAppSettingsService _settings;

    public GetStringAppSettingQueryHandler(IAppSettingsService settings)
    {
        _settings = settings;
    }

    public async Task<Result<string, Error>> Handle(GetStringAppSettingQuery request,
        CancellationToken cancellationToken)
    {
        var value = await _settings.GetValueAsync(request.Key, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? request.DefaultValue : value;
    }
}