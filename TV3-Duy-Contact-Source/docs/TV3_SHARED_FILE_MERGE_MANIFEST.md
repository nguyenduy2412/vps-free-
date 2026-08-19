# Manifest merge file shared — TV3 Contact Request

## Nguyên tắc

Không copy/ghi đè nguyên file shared từ archive vào `dev`. Trên clone Git thật, áp dụng từng dòng tích hợp Contact bên dưới vào phiên bản mới nhất của `dev`, sau đó review `git diff` trước commit.

## Dòng tích hợp cần có

| File shared | Phần TV3 cần giữ | Không nhận là code TV3 |
|---|---|---|
| `CloudServiceStoreDbContext.cs` | `DbSet<ContactRequest>`, `DbSet<ContactRequestStatusHistory>` và apply configuration Contact | Các DbSet/configuration Catalog, Auth, Order, Affiliate, News và Landing có sẵn. |
| `DependencyInjection.cs` | Đăng ký `IContactRequestRepository` / `ContactRequestRepository`, `IContactRequestService` / `ContactRequestService` | Toàn bộ DI Auth, Catalog, Order, Affiliate, Promotion và seed nền. |
| `Program.cs` | `AddContactRequestRateLimiting`, policy `ManageContactRequests`, cờ local seed TV3 (nếu nhóm đồng ý) | Authentication/JWT, các policy khác, middleware nền. |
| `appsettings.json` | `RateLimiting:ContactPermitLimit`, `Seed:Tv3LocalDemoData` mặc định `false` | Connection string, JWT, origin và các limit module khác; phải lấy từ environment/secret của nhóm. |
| `frontend/src/lib/api.ts` | Type `ContactRequest*`, helper query và `contactRequestsApi` dùng `POST /api/v1/contact-requests/{id}/status` | `apiFetch`, auth-refresh wrapper và client module khác. |
| `docker-compose.yml` | `Seed__Tv3LocalDemoData` optional, mặc định `false` | Service image, password, volume, port của foundation; không đổi nếu không có thống nhất nhóm. |

## Contact rate limiter

File `ContactRequestRateLimitExtensions.cs` là file TV3 độc lập. `Program.cs` chỉ cần một lời gọi đăng ký extension. Không sửa `AuthSecurityExtensions.cs` để tránh làm thay đổi body/status 429 của Login/Order/Affiliate.

## Cách review trên branch thật

```powershell
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management
# Chép file mới, sau đó merge các file shared theo từng phần ở bảng trên.
git diff --check
git diff -- src/CloudServiceStore.WebApi/Program.cs src/CloudServiceStore.Infrastructure/DependencyInjection.cs src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs frontend/src/lib/api.ts docker-compose.yml
```

Nếu diff file shared chứa thay đổi thuộc Auth, Order, Affiliate, Catalog, Promotion hoặc Dashboard mà không được liệt kê ở cột TV3, loại chúng khỏi branch trước commit.
