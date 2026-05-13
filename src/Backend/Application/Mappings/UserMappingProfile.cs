using Application.Common.Dtos;
using AutoMapper;
using Domains.UserAggregate;

namespace Application.Mappings;

public sealed class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserSummaryDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value));
    }
}
