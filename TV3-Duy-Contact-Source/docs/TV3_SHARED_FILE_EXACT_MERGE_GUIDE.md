# Merge hunk TV3 Contact Request

> Chỉ áp dụng trên branch `feature/contact-request-management` được tạo từ `dev` mới nhất. Copy file Contact trước, rồi merge **từng hunk** dưới đây. Không thay nguyên `Program.cs`, DbContext, DI, appsettings, `api.ts`, admin-nav hay Docker Compose.

## Hunk bắt buộc

| File shared đích | Hunk TV3 cần có | Không được làm |
|---|---|---|
| `CloudServiceStoreDbContext.cs` | Thêm hai DbSet Contact | Không thêm `ApplyConfiguration` thủ công nếu baseline đã dùng `ApplyConfigurationsFromAssembly` |
| `DependencyInjection.cs` | Đăng ký repository và service Contact scoped | Không đổi đăng ký module khác |
| `Program.cs` | Thêm named rate-limit policy Contact và policy Admin/Editor | Không gọi `AddRateLimiter`/`UseRateLimiter` lần hai; không đổi JWT/CORS/auth |
| `appsettings.json` | Thêm `ContactPermitLimit` | Không thay connection string/secrets |
| `frontend/src/lib/api.ts` | Thêm Contact DTO/API dùng route relative | Giữ `POST` cho status vì controller hiện dùng POST |
| `admin-nav.ts` | Thêm đúng route Admin/Editor Contact | Không sửa item/role module khác |
| `docker-compose.yml` | Healthcheck SQL Server chỉ dùng `SELECT 1`; migrator one-shot | Không đổi image, port, volume hay connection string |

### 1. DbContext

Chèn cạnh các DbSet nghiệp vụ hiện có:

```csharp
public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
public DbSet<ContactRequestStatusHistory> ContactRequestStatusHistories => Set<ContactRequestStatusHistory>();
```

### 2. Dependency injection

Chèn cạnh registration Affiliate/Order hiện có:

```csharp
services.AddScoped<IContactRequestRepository, ContactRequestRepository>();
services.AddScoped<IContactRequestService, ContactRequestService>();
```

### 3. Program.cs: rate limit và authorization

Ngay sau limiter nền:

```csharp
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.ConfigureContactRequestRateLimiting(builder.Configuration);
```

`ConfigureContactRequestRateLimiting` chỉ thêm named policy `contact-requests`. Baseline vẫn sở hữu `AddRateLimiter`, `GlobalLimiter`, `OnRejected`, `ProblemDetails` 429 và một lần `app.UseRateLimiter()`.

Trong `AddAuthorization` thêm:

```csharp
options.AddPolicy("ManageContactRequests", policy => policy.RequireRole("Admin", "Editor"));
```

Khi deploy sau reverse proxy, baseline phải đặt `UseForwardedHeaders` với trusted proxy/network **trước** rate limiter; không dùng header IP do client tự gửi.

### 4. appsettings.json

Trong object `RateLimiting`, thêm một giá trị dương:

```json
"ContactPermitLimit": 5
```

### 5. Frontend API

Thêm type `ContactRequestStatus`, confirmation/list/detail/history và `allowedTransitions` vào `ContactRequestDetail`. API client dùng relative routes:

```ts
create: body => apiFetch<ContactRequestConfirmation>("/api/v1/contact-requests", { method: "POST", body: JSON.stringify(body) }),
detail: id => apiFetch<ContactRequestDetail>(`/api/v1/contact-requests/${id}`),
updateStatus: (id, status, note) => apiFetch<ContactRequestDetail>(`/api/v1/contact-requests/${id}/status`, { method: "POST", body: JSON.stringify({ status, note: note || null }) }),
```

Frontend chỉ hiển thị `allowedTransitions` backend trả về; không tự hard-code workflow.

### 6. Admin navigation

Thêm `IconMessageCircle` và đúng item sau vào group vận hành:

```ts
{
  title: "Yêu cầu liên hệ",
  href: "/admin/contact-requests",
  icon: IconMessageCircle,
  roles: ["Admin", "Editor"],
},
```

### 7. Docker database rỗng

Healthcheck SQL Server phải kiểm tra server sẵn sàng, không đòi database đã tồn tại:

```yaml
-Q "SELECT 1" || exit 1
```

Migrator là one-shot task:

```yaml
restart: "no"
```

## Migration Contact-only

Sau khi các hunk đã merge và build/test pass trên baseline thật, chạy:

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure \
  --startup-project src/CloudServiceStore.WebApi \
  --output-dir Persistence/Migrations
```

Chỉ chấp nhận migration tạo bảng `ContactRequests`, `ContactRequestStatusHistories`, index Contact và FK Contact. FK từ Contact đến `AppUsers` là hợp lệ; tạo/drop/alter trực tiếp bảng Auth, News, Order, Promotion hoặc Affiliate là không hợp lệ. Nếu có model drift, xóa migration vừa sinh và sửa baseline/hunk; không sửa migration bằng tay.

## Audit trước khi commit

```bash
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs \
  src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/appsettings.json \
  frontend/src/lib/api.ts \
  frontend/src/components/admin/admin-nav.ts \
  docker-compose.yml
```

Diff shared chỉ được chứa hunk Contact nêu trên. Nếu thấy Auth/JWT, refresh token, News, Landing, Order, Affiliate, dashboard, container image, port hoặc volume ngoài hunk, dừng và loại thay đổi đó.
