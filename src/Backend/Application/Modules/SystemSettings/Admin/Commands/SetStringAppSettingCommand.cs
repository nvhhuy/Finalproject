using Application.Contracts.Authorization;
using Application.Contracts.Settings;
using MediatR;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.SystemSettings.Admin.Commands;

public sealed record SetStringAppSettingCommand(string Key, string Value) : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class SetStringAppSettingCommandHandler : IRequestHandler<SetStringAppSettingCommand, Result<bool, Error>>
{
    private readonly IAppSettingsService _settings;

    public SetStringAppSettingCommandHandler(IAppSettingsService settings) => _settings = settings;

    public async Task<Result<bool, Error>> Handle(SetStringAppSettingCommand request, CancellationToken cancellationToken)
    {
        await _settings.SetValueAsync(request.Key, request.Value, cancellationToken);
        return true;
    }
}
