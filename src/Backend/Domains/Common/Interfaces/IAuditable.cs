using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;

namespace Domains.Common.Interfaces
{
    public interface IAuditable
    {
        User? CreatedBy { get; set; }
        UserId? CreatedByUserId { get; set; }
        DateTime CreatedOn { get; set; }
        User? LastModifiedBy { get; set; }
        UserId? LastModifiedByUserId { get; set; }
        DateTime? LastModifiedOn { get; set; }
    }
}
