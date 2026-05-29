using Riok.Mapperly.Abstractions;

[assembly: MapperDefaults(
    RequiredMappingStrategy = RequiredMappingStrategy.Target,
    IncludedMembers = MemberVisibility.AllAccessible,
    UseDeepCloning = false)]