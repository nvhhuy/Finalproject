using Application.Contracts.Settings;
using MediatR;
using Shared.Common;
using Shared.Errors;

namespace Application.Modules.SystemSettings.Admin.Commands;

public sealed record SetBooleanAppSettingCommand(string Key, bool Value) : IRequest<Result<bool, Error>>;

public sealed class SetBooleanAppSettingCommandHandler : IRequestHandler<SetBooleanAppSettingCommand, Result<bool, Error>>
{
    private readonly IAppSettingsService _settings;

    public SetBooleanAppSettingCommandHandler(IAppSettingsService settings) => _settings = settings;

    public async Task<Result<bool, Error>> Handle(SetBooleanAppSettingCommand request, CancellationToken cancellationToken)
    {
        await _settings.SetValueAsync(request.Key, request.Value.ToString(), cancellationToken);
        return true;
    }
}
