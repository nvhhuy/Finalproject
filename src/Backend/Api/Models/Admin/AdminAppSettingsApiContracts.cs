namespace Api.Models.Admin;

public sealed record SetBooleanSettingRequest(bool Value);

public sealed record BooleanSettingDto(string Key, bool Value);
