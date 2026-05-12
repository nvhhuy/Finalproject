using Domains.Common.Interfaces;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;

namespace Domains.Common
{
    public abstract class Auditable<TId> : Entity<TId>, IAuditable where TId : notnull
    {
        public User? CreatedBy { get; set; }
        public UserId? CreatedByUserId { get; set; }
        public DateTime CreatedOn { get; set; }
        public User? LastModifiedBy { get; set; }
        public UserId? LastModifiedByUserId { get; set; }
        public DateTime? LastModifiedOn { get; set; }

    }
}
