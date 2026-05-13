using Application.Contracts.Settings;
using MediatR;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.SystemSettings.Admin.Queries;

public sealed record GetBooleanAppSettingQuery(string Key, bool DefaultValue) : IRequest<Result<bool, Error>>;

public sealed class GetBooleanAppSettingQueryHandler : IRequestHandler<GetBooleanAppSettingQuery, Result<bool, Error>>
{
    private readonly IAppSettingsService _settings;

    public GetBooleanAppSettingQueryHandler(IAppSettingsService settings) => _settings = settings;

    public async Task<Result<bool, Error>> Handle(GetBooleanAppSettingQuery request, CancellationToken cancellationToken)
    {
        bool value = await _settings.GetBooleanAsync(request.Key, request.DefaultValue, cancellationToken);
        return value;
    }
}
