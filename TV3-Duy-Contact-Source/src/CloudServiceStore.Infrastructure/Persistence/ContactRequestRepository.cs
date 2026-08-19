using System.Data;
using System.Security.Cryptography;
using System.Text;
using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CloudServiceStore.Infrastructure.Persistence;

public sealed class ContactRequestRepository(CloudServiceStoreDbContext db)
    : IContactRequestRepository
{
    public async Task<bool> TryCreateAsync(
        ContactRequest request,
        AuditLog audit,
        DateTimeOffset createdAfter,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            var existingInNonSqlProvider = await db.ContactRequests.AnyAsync(
                item => item.Email == request.Email && item.CreatedAt >= createdAfter,
                cancellationToken);
            if (existingInNonSqlProvider)
                return false;

            db.ContactRequests.Add(request);
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (!await AcquireEmailLockAsync(request.Email, cancellationToken))
            return false;

        var alreadyExists = await db.ContactRequests.AnyAsync(
            item => item.Email == request.Email && item.CreatedAt >= createdAfter,
            cancellationToken);
        if (alreadyExists)
            return false;

        db.ContactRequests.Add(request);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

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

    public void AddAudit(
        Guid? actorId,
        string action,
        string entityName,
        Guid entityId,
        string? oldValuesJson,
        string? newValuesJson,
        DateTimeOffset occurredAt,
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
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            CreatedBy = actorId
        });

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    private async Task<bool> AcquireEmailLockAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;

        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.DbType = DbType.String;
        resource.Value = $"ContactRequest:Email:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)))}";
        command.Parameters.Add(resource);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && Convert.ToInt32(result) >= 0;
    }
}
