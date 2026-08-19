# Audit health check, migrator và E2E — Contact TV3

## Phần đã có trong source

| Yêu cầu | Evidence source hiện tại |
|---|---|
| SQL Server healthcheck | `docker-compose.yml` có `sqlserver.healthcheck` dùng `sqlcmd`, interval/retry/start period. |
| Migrator độc lập | `docker-compose.yml` có service `migrator`, target `migrator` của `deploy/api.Dockerfile`, chạy `dotnet ef database update`; API chờ `service_completed_successfully`. |
| API health | `Program.cs` gọi `AddHealthChecks()` và `MapHealthChecks("/health")`. |
| Rate limit Contact | `AddContactRequestRateLimiting()` trong Program; public create dùng named policy theo IP. |
| Audit/workflow | Contact service tạo status history/audit, workflow `Pending → Contacted → Approved` hoặc `Rejected/Cancelled`, terminal không reopen. |
| DTO/ProblemDetails | Controller nhận DTO, gọi service, map lỗi sang ProblemDetails; không trả EF entity. |

## E2E mới bổ sung

`frontend/e2e/contact-public.spec.ts` dùng Playwright và chạy với ba viewport/browser project:

1. Desktop Chromium.
2. iPhone 13 Chromium.
3. Viewport Chromium 360 × 800.

Các test mock `POST /api/v1/contact-requests` ở browser layer để không phụ thuộc Docker/database, rồi xác minh public form submit success/reset, `ProblemDetails` 400/409/429, native required/email validation, network failure, loading/double-submit và không có horizontal overflow ở 360px. Lệnh `npm run test:e2e:contact` đã pass **27/27**.

## Không áp dụng snippet compose cũ

Không thay compose/source bằng snippet dùng `CloudServiceStore.Api`, `ApplicationDbContext`, `DefaultConnection`, `New/InProgress/Resolved` hoặc password hard-code. Các tên đó không khớp contract/source hiện tại và làm sai pipeline migrator riêng đã có.

## Việc còn lại trước merge

1. Sinh migration Contact-only từ clone `dev` thật bằng `dotnet ef`.
2. Chạy `docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml up --build --detach` trên máy có Docker.
3. Chứng minh migrator exit `0`, API `/health` trả 200, và lưu screenshot responsive nếu hồ sơ yêu cầu ảnh.
4. Chạy CI GitHub và xin review thực chất từ TV2.
