using Domains.UserAggregate.ValueObjects;
using Riok.Mapperly.Abstractions;

namespace Application.Mapping;

/// <summary>
/// Shared value-object and primitive conversions reused by all Mapperly profiles in this assembly.
/// Reference via <c>[Mapper(UseMapper = typeof(MappingConversions))]</c> — no duplicate config per mapper.
/// </summary>
[Mapper]
public static partial class MappingConversions
{
    public static Guid ToGuid(UserId id) => id.Value;
}
