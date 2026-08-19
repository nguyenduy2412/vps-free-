using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudServiceStore.Infrastructure.Persistence;

public sealed class ContactRequestRepository(CloudServiceStoreDbContext db)
    : IContactRequestRepository
{
    public Task<bool> HasRecentRequestAsync(
        string email,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken) =>
        db.ContactRequests.AnyAsync(
            item => item.Email == email && item.CreatedAt >= createdAfter,
            cancellationToken);

    public async Task<(IReadOnlyList<ContactRequest> Items, int Total)> GetAsync(
        ContactRequestQuery query,
        CancellationToken cancellationToken)
    {
        var itemsQuery = db.ContactRequests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            itemsQuery = itemsQuery.Where(item =>
                item.FullName.Contains(search) ||
                item.Email.Contains(search) ||
                item.Subject.Contains(search) ||
                (item.CompanyName != null && item.CompanyName.Contains(search)));
        }

        if (query.Status is { } status)
            itemsQuery = itemsQuery.Where(item => item.Status == status);

        if (query.CreatedFrom is { } createdFrom)
            itemsQuery = itemsQuery.Where(item => item.CreatedAt >= createdFrom);

        if (query.CreatedTo is { } createdTo)
            itemsQuery = itemsQuery.Where(item => item.CreatedAt <= createdTo);

        var total = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderByDescending(item => item.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ContactRequest?> FindAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        db.ContactRequests
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public void Add(ContactRequest request) => db.ContactRequests.Add(request);

    public void AddStatusHistory(ContactRequestStatusHistory history) =>
        db.ContactRequestStatusHistories.Add(history);

    public void AddAudit(
        Guid? actorId,
        string action,
        string entityName,
        Guid entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? ipAddress) =>
        db.AuditLogs.Add(new AuditLog
        {
            AppUserId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            IpAddress = ipAddress,
            OccurredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actorId
        });

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
