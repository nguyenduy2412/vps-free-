using CloudServiceStore.Domain.Common;
using CloudServiceStore.Domain.Enums;

namespace CloudServiceStore.Domain.Entities;

public sealed class ContactRequest : AuditableEntity
{
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.Pending;
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public ICollection<ContactRequestStatusHistory> StatusHistory { get; } =
        new List<ContactRequestStatusHistory>();
}

public sealed class ContactRequestStatusHistory : AuditableEntity
{
    public Guid ContactRequestId { get; set; }
    public ContactRequest ContactRequest { get; set; } = null!;

    public ContactRequestStatus FromStatus { get; set; }
    public ContactRequestStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public Guid? ChangedBy { get; set; }
}
