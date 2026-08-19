using CloudServiceStore.Domain.Entities;

namespace CloudServiceStore.Application.ContactRequests;

public interface IContactRequestRepository
{
    Task<bool> TryCreateAsync(
        ContactRequest request,
        AuditLog audit,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ContactRequest> Items, int Total)> GetAsync(
        ContactRequestQuery query,
        CancellationToken cancellationToken);

    Task<ContactRequest?> FindAsync(
        Guid id,
        CancellationToken cancellationToken);

    void AddAudit(
        Guid? actorId,
        string action,
        string entityName,
        Guid entityId,
        string? oldValuesJson,
        string? newValuesJson,
        DateTimeOffset occurredAt,
        string? ipAddress);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ContactRequestNotFoundException(string message) : Exception(message);

public sealed class ContactRequestConflictException(string message) : Exception(message);

public sealed class ContactRequestValidationException(string message) : Exception(message);

public sealed class ContactRequestLockUnavailableException(string message) : Exception(message);
