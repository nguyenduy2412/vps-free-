# Hướng dẫn triển khai `feature/contact-request-management`

## 1. Phạm vi chính xác của module

Module này thuộc phần việc của TV3/Duy, không phải `feature/auth-hardening` của TV1. Mục tiêu là tiếp nhận yêu cầu liên hệ/tư vấn từ website và cung cấp màn hình quản trị cho Admin/Editor xử lý trạng thái. Không đưa thanh toán, CRM, gửi email thật, chatbot hoặc đồng bộ bên thứ ba vào MVP nếu nhóm chưa chốt trong quy ước.

API contract tối thiểu cần giữ đúng là:

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| `POST` | `/api/v1/contact-requests` | Public | Khách gửi yêu cầu liên hệ/tư vấn |
| `GET` | `/api/v1/contact-requests` | `ManageContactRequests` | Admin/Editor xem danh sách, lọc và phân trang |
| `GET` | `/api/v1/contact-requests/{id}` | `ManageContactRequests` | Admin/Editor xem chi tiết và lịch sử |
| `PATCH` | `/api/v1/contact-requests/{id}/status` | `ManageContactRequests` | Admin/Editor đổi trạng thái |

Nếu bảng quy ước nhóm đã chốt tên enum hoặc field khác, phải giữ tên trong quy ước; các đoạn dưới đây là mẫu triển khai nhất quán với `AffiliateApplication` và `OrderRequest` đang có trong repository.

## 2. Tạo branch và kiểm tra baseline

Từ thư mục gốc chứa solution, tạo branch riêng:

```powershell
git checkout -b feature/contact-request-management
git status
```

Trước khi viết code, kiểm tra các mẫu đã có:

```text
src/CloudServiceStore.Application/Affiliates/
src/CloudServiceStore.Infrastructure/Persistence/AffiliateRepository.cs
src/CloudServiceStore.Infrastructure/Persistence/Configurations/AffiliateConfigurations.cs
src/CloudServiceStore.WebApi/Controllers/AffiliatesController.cs
frontend/src/components/affiliate-public-client.tsx
frontend/src/components/admin-affiliates-client.tsx
```

Không tạo một tầng Repository, một kiểu `Result` hoặc một hệ thống phân quyền mới. Contact Request phải dùng cùng `PagedResult<T>`, ProblemDetails, `AuditableEntity`, `AuditLog`, policy và frontend API proxy hiện có.

## 3. Domain: enum, entity và lịch sử trạng thái

Tạo `src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs`. Nếu nhóm đã chốt enum khác, thay đúng tên nhưng giữ các trạng thái nghiệp vụ tương đương.

```csharp
namespace CloudServiceStore.Domain.Enums;

public enum ContactRequestStatus
{
    New = 1,
    InProgress = 2,
    Resolved = 3,
    Rejected = 4
}
```

Tạo `src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs`. Dùng `AppUserId` nullable vì khách gửi form có thể chưa đăng nhập; không lưu password, token hoặc dữ liệu thẻ.

```csharp
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
    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.New;
    public string? ResolutionNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public ICollection<ContactRequestStatusHistory> StatusHistory { get; } = new List<ContactRequestStatusHistory>();
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
```

## 4. EF Core configuration và DbContext

Tạo `src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs`:

```csharp
using CloudServiceStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudServiceStore.Infrastructure.Persistence.Configurations;

public sealed class ContactRequestConfiguration : IEntityTypeConfiguration<ContactRequest>
{
    public void Configure(EntityTypeBuilder<ContactRequest> builder)
    {
        builder.ToTable("ContactRequests");
        builder.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CompanyName).HasMaxLength(160);
        builder.Property(x => x.Subject).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.Email, x.CreatedAt });
        builder.HasIndex(x => new { x.AppUserId, x.CreatedAt });
        builder.HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContactRequestStatusHistoryConfiguration
    : IEntityTypeConfiguration<ContactRequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<ContactRequestStatusHistory> builder)
    {
        builder.ToTable("ContactRequestStatusHistories");
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ContactRequestId, x.CreatedAt });
        builder.HasOne(x => x.ContactRequest)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ContactRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Trong `src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs`, thêm hai DbSet cạnh các DbSet nghiệp vụ khác:

```csharp
public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
public DbSet<ContactRequestStatusHistory> ContactRequestStatusHistories => Set<ContactRequestStatusHistory>();
```

`ApplyConfigurationsFromAssembly` đã được dùng trong DbContext nên không cần đăng ký từng configuration bằng tay.

## 5. Application contracts và abstraction

Tạo `src/CloudServiceStore.Application/ContactRequests/ContactRequestContracts.cs`:

```csharp
using CloudServiceStore.Application.Common;
using CloudServiceStore.Domain.Enums;

namespace CloudServiceStore.Application.ContactRequests;

public sealed record CreateContactRequestRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string Subject,
    string Message);

public sealed record ContactRequestQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    ContactRequestStatus? Status = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null);

public sealed record UpdateContactRequestStatusRequest(
    ContactRequestStatus Status,
    string? Note);

public sealed record ContactRequestConfirmationDto(
    Guid Id,
    ContactRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string Subject,
    ContactRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestStatusHistoryDto(
    Guid Id,
    ContactRequestStatus FromStatus,
    ContactRequestStatus ToStatus,
    string? Note,
    Guid? ChangedBy,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestDetailDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string Subject,
    string Message,
    ContactRequestStatus Status,
    string? ResolutionNote,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ContactRequestStatusHistoryDto> StatusHistory);

public interface IContactRequestService
{
    Task<ContactRequestConfirmationDto> CreateAsync(
        CreateContactRequestRequest request,
        Guid? ownerId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedResult<ContactRequestListItemDto>> GetAsync(
        ContactRequestQuery query,
        CancellationToken cancellationToken);

    Task<ContactRequestDetailDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ContactRequestDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}
```

Tạo `ContactRequestAbstractions.cs`:

```csharp
using CloudServiceStore.Domain.Entities;

namespace CloudServiceStore.Application.ContactRequests;

public interface IContactRequestRepository
{
    Task<bool> HasRecentRequestAsync(string email, DateTimeOffset createdAfter, CancellationToken ct);
    Task<(IReadOnlyList<ContactRequest> Items, int Total)> GetAsync(ContactRequestQuery query, CancellationToken ct);
    Task<ContactRequest?> FindAsync(Guid id, CancellationToken ct);
    void Add(ContactRequest request);
    void AddStatusHistory(ContactRequestStatusHistory history);
    void AddAudit(Guid? actorId, string action, string entityName, Guid entityId,
        string? oldValuesJson, string? newValuesJson, string? ipAddress);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class ContactRequestNotFoundException(string message) : Exception(message);
public sealed class ContactRequestConflictException(string message) : Exception(message);
public sealed class ContactRequestValidationException(string message) : Exception(message);
```

## 6. Application service

Tạo `ContactRequestService.cs` theo mẫu `AffiliateService`. Các quy tắc bắt buộc là normalize email, validate độ dài, chặn gửi trùng trong khoảng thời gian ngắn, tạo status history đầu tiên và ghi audit. Không ghi nội dung message đầy đủ vào audit để tránh nhân bản dữ liệu cá nhân.

```csharp
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudServiceStore.Application.Common;
using CloudServiceStore.Domain.Entities;
using CloudServiceStore.Domain.Enums;

namespace CloudServiceStore.Application.ContactRequests;

public sealed partial class ContactRequestService(
    IContactRequestRepository repository,
    TimeProvider? timeProvider = null) : IContactRequestService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<ContactRequestConfirmationDto> CreateAsync(
        CreateContactRequestRequest request,
        Guid? ownerId,
        string? ipAddress,
        CancellationToken ct)
    {
        ValidateCreate(request);
        var now = clock.GetUtcNow();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await repository.HasRecentRequestAsync(email, now.AddHours(-24), ct))
            throw new ContactRequestConflictException(
                "A contact request with this email was submitted recently.");

        var item = new ContactRequest
        {
            AppUserId = ownerId,
            CreatedBy = ownerId,
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = NormalizePhone(request.PhoneNumber),
            CompanyName = CleanOptional(request.CompanyName),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            Status = ContactRequestStatus.New,
            CreatedAt = now
        };

        var history = new ContactRequestStatusHistory
        {
            ContactRequestId = item.Id,
            FromStatus = ContactRequestStatus.New,
            ToStatus = ContactRequestStatus.New,
            Note = "Contact request received.",
            ChangedBy = ownerId,
            CreatedBy = ownerId,
            CreatedAt = now
        };
        item.StatusHistory.Add(history);
        repository.Add(item);
        repository.AddStatusHistory(history);
        repository.AddAudit(ownerId, "ContactRequest.Created", nameof(ContactRequest), item.Id,
            null, JsonSerializer.Serialize(new { item.Status, item.Email }), ipAddress);
        await repository.SaveChangesAsync(ct);
        return new(item.Id, item.Status, item.CreatedAt);
    }

    public async Task<PagedResult<ContactRequestListItemDto>> GetAsync(
        ContactRequestQuery query, CancellationToken ct)
    {
        ValidateQuery(query);
        var result = await repository.GetAsync(query, ct);
        return new(result.Items.Select(MapList).ToArray(), query.Page, query.PageSize, result.Total);
    }

    public async Task<ContactRequestDetailDto?> GetByIdAsync(Guid id, CancellationToken ct) =>
        (await repository.FindAsync(id, ct)) is { } item ? MapDetail(item) : null;

    public async Task<ContactRequestDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken ct)
    {
        if (actorId == Guid.Empty || !Enum.IsDefined(request.Status))
            throw new ContactRequestValidationException("A valid actor and status are required.");
        if (request.Note?.Trim().Length > 1000)
            throw new ContactRequestValidationException("Note must be 1000 characters or fewer.");

        var item = await repository.FindAsync(id, ct)
            ?? throw new ContactRequestNotFoundException("Contact request was not found.");
        if (item.Status == request.Status)
            throw new ContactRequestConflictException("The request already has this status.");
        if (!CanTransition(item.Status, request.Status))
            throw new ContactRequestConflictException(
                $"Cannot change status from {item.Status} to {request.Status}.");

        var previous = item.Status;
        var now = clock.GetUtcNow();
        var note = CleanOptional(request.Note);
        item.Status = request.Status;
        item.ResolutionNote = note;
        item.ResolvedBy = request.Status is ContactRequestStatus.Resolved or ContactRequestStatus.Rejected
            ? actorId : null;
        item.ResolvedAt = request.Status is ContactRequestStatus.Resolved or ContactRequestStatus.Rejected
            ? now : null;
        item.UpdatedBy = actorId;
        item.UpdatedAt = now;

        var history = new ContactRequestStatusHistory
        {
            ContactRequestId = item.Id,
            FromStatus = previous,
            ToStatus = request.Status,
            Note = note,
            ChangedBy = actorId,
            CreatedBy = actorId,
            CreatedAt = now
        };
        item.StatusHistory.Add(history);
        repository.AddStatusHistory(history);
        repository.AddAudit(actorId, "ContactRequest.StatusChanged", nameof(ContactRequest), item.Id,
            JsonSerializer.Serialize(new { Status = previous }),
            JsonSerializer.Serialize(new { Status = item.Status, Note = note }), ipAddress);
        await repository.SaveChangesAsync(ct);
        return MapDetail(item);
    }

    private static bool CanTransition(ContactRequestStatus from, ContactRequestStatus to) => from switch
    {
        ContactRequestStatus.New => to is ContactRequestStatus.InProgress or ContactRequestStatus.Resolved or ContactRequestStatus.Rejected,
        ContactRequestStatus.InProgress => to is ContactRequestStatus.Resolved or ContactRequestStatus.Rejected,
        _ => false
    };

    private static void ValidateCreate(CreateContactRequestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 160)
            throw new ContactRequestValidationException("Full name is required and must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 256 || !IsValidEmail(request.Email))
            throw new ContactRequestValidationException("A valid email address is required.");
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || !PhonePattern().IsMatch(request.PhoneNumber.Trim()))
            throw new ContactRequestValidationException("A valid phone number from 8 to 15 digits is required.");
        if (request.CompanyName?.Trim().Length > 160)
            throw new ContactRequestValidationException("Company name must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 180)
            throw new ContactRequestValidationException("Subject is required and must be 180 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000)
            throw new ContactRequestValidationException("Message is required and must be 4000 characters or fewer.");
    }

    private static void ValidateQuery(ContactRequestQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new ContactRequestValidationException("Page must be at least 1 and PageSize must be from 1 to 100.");
        if (query.Search?.Trim().Length > 256)
            throw new ContactRequestValidationException("Search must be 256 characters or fewer.");
        if (query.Status is not null && !Enum.IsDefined(query.Status.Value))
            throw new ContactRequestValidationException("Contact request status is not supported.");
        if (query.CreatedFrom is not null && query.CreatedTo is not null && query.CreatedTo < query.CreatedFrom)
            throw new ContactRequestValidationException("CreatedTo must be on or after CreatedFrom.");
    }

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value.Trim()).Address == value.Trim(); }
        catch { return false; }
    }

    private static string NormalizePhone(string value) => Regex.Replace(value.Trim(), "[\\s().-]", "");
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    [GeneratedRegex(@"^\\+?(?:[\\d][\\s().-]*){8,15}$")]
    private static partial Regex PhonePattern();

    private static ContactRequestListItemDto MapList(ContactRequest x) =>
        new(x.Id, x.FullName, x.Email, x.PhoneNumber, x.CompanyName, x.Subject, x.Status, x.CreatedAt);

    private static ContactRequestDetailDto MapDetail(ContactRequest x) =>
        new(x.Id, x.FullName, x.Email, x.PhoneNumber, x.CompanyName, x.Subject, x.Message,
            x.Status, x.ResolutionNote, x.ResolvedBy, x.ResolvedAt, x.CreatedAt, x.UpdatedAt,
            x.StatusHistory.OrderBy(h => h.CreatedAt).Select(h =>
                new ContactRequestStatusHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.Note, h.ChangedBy, h.CreatedAt)).ToArray());
}
```

Trong code thật, `ContactRequestService` nên được chia nhỏ nếu nhóm có rule style giới hạn độ dài file. Quan trọng nhất là không bỏ qua validation, status history, audit và duplicate-window.

## 7. Repository Infrastructure

Tạo `src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs`. Sao chép pattern của `AffiliateRepository`: `AsNoTracking` cho list, `Include` cho detail, `CountAsync`, `Skip/Take` và `SaveChangesAsync` qua DbContext.

```csharp
using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudServiceStore.Infrastructure.Persistence;

public sealed class ContactRequestRepository(CloudServiceStoreDbContext db)
    : IContactRequestRepository
{
    public Task<bool> HasRecentRequestAsync(string email, DateTimeOffset createdAfter, CancellationToken ct) =>
        db.ContactRequests.AnyAsync(x => x.Email == email && x.CreatedAt >= createdAfter, ct);

    public async Task<(IReadOnlyList<ContactRequest> Items, int Total)> GetAsync(
        ContactRequestQuery query, CancellationToken ct)
    {
        var source = db.ContactRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.FullName.Contains(search)
                || x.Email.Contains(search)
                || x.Subject.Contains(search));
        }
        if (query.Status is not null) source = source.Where(x => x.Status == query.Status);
        if (query.CreatedFrom is not null) source = source.Where(x => x.CreatedAt >= query.CreatedFrom);
        if (query.CreatedTo is not null) source = source.Where(x => x.CreatedAt <= query.CreatedTo);

        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<ContactRequest?> FindAsync(Guid id, CancellationToken ct) =>
        db.ContactRequests.Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public void Add(ContactRequest request) => db.ContactRequests.Add(request);
    public void AddStatusHistory(ContactRequestStatusHistory history) => db.ContactRequestStatusHistories.Add(history);

    public void AddAudit(Guid? actorId, string action, string entityName, Guid entityId,
        string? oldValuesJson, string? newValuesJson, string? ipAddress) =>
        db.AuditLogs.Add(new AuditLog
        {
            AppUserId = actorId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            IpAddress = ipAddress,
            OccurredAt = DateTimeOffset.UtcNow
        });

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

Tên property của `AuditLog` phải đối chiếu với entity thật trong repository. Không tự đổi tên property nếu source hiện tại đang dùng tên khác.

## 8. Đăng ký DI

Trong phần cấu hình Infrastructure nơi nhóm đã đăng ký `IAffiliateRepository`, thêm:

```csharp
services.AddScoped<IContactRequestRepository, ContactRequestRepository>();
services.AddScoped<IContactRequestService, ContactRequestService>();
```

Không đăng ký service ở Controller và không gọi DbContext trực tiếp từ Web API.

## 9. Web API controller và policy

Nếu Program.cs đã có policy `ManageOrders` hoặc `ManageAffiliates`, nên tạo policy riêng rõ nghĩa:

```csharp
options.AddPolicy("ManageContactRequests", policy =>
    policy.RequireRole("Admin", "Editor"));
```

Tạo `src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs`:

```csharp
using System.Security.Claims;
using CloudServiceStore.Application.ContactRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudServiceStore.WebApi.Controllers;

[ApiController]
[Route("api/v1/contact-requests")]
public sealed class ContactRequestsController(IContactRequestService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public Task<IActionResult> Create(CreateContactRequestRequest request, CancellationToken ct) =>
        Execute(async () =>
        {
            var result = await service.CreateAsync(request, GetOptionalUserId(), GetIpAddress(), ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        });

    [Authorize(Policy = "ManageContactRequests")]
    [HttpGet]
    public Task<IActionResult> Get([FromQuery] ContactRequestQuery query, CancellationToken ct) =>
        Execute(async () => Ok(await service.GetAsync(query, ct)));

    [Authorize(Policy = "ManageContactRequests")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        (await service.GetByIdAsync(id, ct)) is { } item
            ? Ok(item)
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Resource not found");

    [Authorize(Policy = "ManageContactRequests")]
    [HttpPatch("{id:guid}/status")]
    public Task<IActionResult> UpdateStatus(Guid id, UpdateContactRequestStatusRequest request, CancellationToken ct) =>
        Execute(async () => Ok(await service.UpdateStatusAsync(id, request, GetActorId(), GetIpAddress(), ct)));

    private Guid? GetOptionalUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : throw new UnauthorizedAccessException();

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ContactRequestNotFoundException ex) { return Problem(404, "Resource not found", ex.Message); }
        catch (ContactRequestConflictException ex) { return Problem(409, "Business conflict", ex.Message); }
        catch (ContactRequestValidationException ex) { return Problem(400, "Validation failed", ex.Message); }
    }
}
```

Nếu compiler không nhận overload `Problem(404, ...)`, dùng cú pháp đã có trong `OrdersController` của source để thống nhất với controller hiện tại.

## 10. Migration SQL Server

Sau khi code compile, chạy từ thư mục gốc solution:

```powershell
dotnet ef migrations add AddContactRequestManagement `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi `
  --output-dir Persistence/Migrations
```

Kiểm tra migration chỉ chứa `ContactRequests`, `ContactRequestStatusHistories` và index/FK liên quan. Không commit migration nếu nó tự động đổi hoặc xóa bảng ngoài phạm vi.

Chạy database:

```powershell
dotnet ef database update `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi
```

Trong Docker, migrator của repository phải được rebuild để migration mới được chạy:

```powershell
docker compose up --build migrator
docker compose up -d api
```

## 11. Frontend public form

Frontend dùng Next.js. Route proxy hiện có ở `frontend/src/app/api/[...path]/route.ts`, vì vậy client nên gọi `/api/v1/contact-requests`, không hard-code URL Docker/API.

Tạo component `frontend/src/components/contact-request-public-client.tsx` với state tối thiểu cho `fullName`, `email`, `phoneNumber`, `companyName`, `subject`, `message`. Submit bằng:

```tsx
const response = await fetch("/api/v1/contact-requests", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ fullName, email, phoneNumber, companyName, subject, message }),
});

const payload = await response.json();
if (!response.ok) {
  setError(payload.detail ?? "Không thể gửi yêu cầu liên hệ.");
  return;
}
setConfirmation(payload);
```

Tạo route `frontend/src/app/contact/page.tsx` chỉ để đặt metadata và render component. Form phải có label, trạng thái loading, lỗi validation, success state hiển thị mã yêu cầu và `aria-live` cho thông báo. Không hiển thị stack trace hoặc exception backend cho khách.

## 12. Frontend admin/editor

Tạo `frontend/src/app/admin/contact-requests/page.tsx` và component `frontend/src/components/admin-contact-requests-client.tsx`. Có các chức năng: lọc status, tìm kiếm, phân trang, mở detail, hiển thị message, xem history và đổi status kèm note. UI chỉ gọi:

```text
GET   /api/v1/contact-requests?page=1&pageSize=20&status=New
GET   /api/v1/contact-requests/{id}
PATCH /api/v1/contact-requests/{id}/status
```

Nút đổi trạng thái phải disable khi request đang loading. Khi nhận `400`, `403`, `404`, `409` hoặc `429`, hiển thị thông báo thân thiện. Không cho frontend tự quyết định quyền; API policy mới là lớp bảo vệ thật.

## 13. Test bắt buộc

### Unit/Application test

Tạo `tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs` và kiểm tra ít nhất:

| Test | Kết quả |
|---|---|
| Email sai | Ném validation exception |
| Message rỗng/quá dài | Ném validation exception |
| Email gửi lại trong 24 giờ | Ném conflict exception |
| Tạo mới | Status `New`, tạo history đầu tiên và audit |
| `New -> InProgress` | Thành công |
| `InProgress -> Resolved` | Thành công, có `ResolvedBy/ResolvedAt` |
| `Resolved -> New` | Conflict |
| Rejected không có note | Validation exception |
| Query page size > 100 | Validation exception |

### Integration/API test

Tạo `tests/CloudServiceStore.Integration.Tests/ContactRequestApiTests.cs` kiểm tra:

```text
POST anonymous /api/v1/contact-requests -> 201
GET anonymous /api/v1/contact-requests -> 401/403
GET Admin/Editor /api/v1/contact-requests -> 200
GET /{id} không tồn tại -> 404
PATCH status với Admin/Editor -> 200
PATCH status với Customer -> 403
PATCH status sai transition -> 409
```

Test phải dùng database test riêng hoặc fixture hiện có của repository, không seed dữ liệu giả vào database production/local của nhóm.

## 14. Kiểm tra trước khi commit

```powershell
dotnet format CloudServiceStore.sln --verify-no-changes
dotnet build CloudServiceStore.sln --configuration Release
dotnet test CloudServiceStore.sln --configuration Release
cd frontend
npm.cmd run lint
npm.cmd run build
```

Rà soát secret:

```powershell
git status --short
git diff --check
git grep -n -I -E "(Password=|JWT_SIGNING_KEY=|api[_-]?key=)" -- ':!.env.example'
```

Không commit `.env`, password SQL Server thật, JWT key thật hoặc dữ liệu liên hệ thật.

## 15. Commit và Pull Request

```powershell
git add src tests frontend/src docs

git commit -m "feat(contact): add contact request management"
git push -u origin feature/contact-request-management
```

PR phải ghi rõ: public submit, admin/editor list/detail/status, status history, audit, migration, test và giới hạn MVP. Nếu thêm email thật, CAPTCHA, file upload hoặc webhook thì phải mở issue/cập nhật quy ước trước, không lặng lẽ mở rộng contract.

## 16. Kịch bản demo TV3

Đầu tiên mở trang `/contact`, nhập tên, email, số điện thoại, chủ đề và nội dung rồi gửi. Ghi lại mã request trả về. Đăng nhập bằng tài khoản Admin hoặc Editor, mở `/admin/contact-requests`, tìm request theo email/chủ đề, mở chi tiết và chuyển `New` sang `InProgress`, sau đó `Resolved` với note. Mở lại detail để chỉ ra status history, `ResolvedAt`, người xử lý và kết quả API. Cuối cùng thử tài khoản Customer gọi màn hình quản trị để chứng minh API trả `403`; đây là bằng chứng phân quyền chứ không chỉ là ẩn nút trên giao diện.

## 17. Tiêu chí nghiệm thu

Module chỉ được xem là hoàn tất khi có đủ entity, configuration, DbSet, migration, application contracts/service, repository, controller, policy, public form, admin/editor UI, unit test, integration test và tài liệu demo. Đặc biệt, không dùng file `auth-hardening-final.zip` làm bản nộp TV3; PR phải có branch `feature/contact-request-management` riêng và review đúng quy trình nhóm.
