# Báo cáo kiểm chứng trước đóng gói — Contact Request

**Ngày kiểm chứng:** 18/08/2026 (GMT+7)
**Phạm vi:** `feature/contact-request-management`, SQL Server, .NET/EF Core, Docker Compose và Next.js frontend.

## Kết quả chạy thực tế

| Hạng mục | Lệnh/điều kiện | Kết quả | Nhận định |
|---|---|---:|---|
| Frontend lint | `frontend && npm run lint` | PASS | ESLint không báo lỗi. |
| Frontend typecheck | `frontend && npx tsc --noEmit` | PASS | Không có lỗi TypeScript, gồm các file Contact Request. |
| Frontend production build | `frontend && npm run build` | BLOCKED | Build dừng khi prerender route có sẵn `/admin/orders`: `TypeError: Cannot read properties of null (reading 'use')`. Đây không phải route Contact Request; cần sửa baseline trước khi xác nhận build toàn dự án. |
| Backend Release build | `dotnet build CloudServiceStore.sln --configuration Release` | NOT RUN | Môi trường kiểm chứng không có lệnh `dotnet`. |
| Backend tests/coverage | `dotnet test ... --collect:"XPlat Code Coverage"` | NOT RUN | Không có .NET SDK 10. |
| EF migration | `dotnet ef migrations add AddContactRequestManagement ...` | NOT RUN | Không có `dotnet` hoặc EF CLI runtime. Chưa có migration Contact Request được sinh trong source. |
| SQL Server Docker | Docker Compose database rỗng | NOT RUN | Môi trường không có `docker`/`docker compose`. |

## Rà soát source tĩnh

Infrastructure đang dùng `Microsoft.EntityFrameworkCore.SqlServer` và `Microsoft.EntityFrameworkCore.Design` phiên bản `10.0.10`; `UseSqlServer` được đăng ký trong dependency injection. `CloudServiceStoreDbContext` đã có hai DbSet Contact Request. Configuration chỉ định hai bảng `ContactRequests`, `ContactRequestStatusHistories`, bốn index request, một index history, FK user `Restrict` và FK history `Cascade`.

Chưa tồn tại file migration có tên Contact Request trong `src/CloudServiceStore.Infrastructure/Persistence/Migrations`. Vì vậy, không được tuyên bố migration đã chạy sạch hay Docker đã được demo; migration thật vẫn là điều kiện chặn đóng gói cuối.

## Lệnh bắt buộc chạy trên máy nhóm

Mở PowerShell tại thư mục chứa `CloudServiceStore.sln`, sau đó chạy theo thứ tự dưới đây. Script chỉ sinh migration khi restore, build và test thành công; hãy review diff migration trước lệnh database update.

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./scripts/Build-And-AddContactRequestMigration.ps1

dotnet ef database update `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi

./scripts/Run-ContactRequestEmptyDatabase.ps1 -Reset
```

Sau đó xác minh `docker compose ps`, endpoint `/health`, `POST /api/v1/contact-requests` trả `201` và API list bằng token Admin/Editor trả `200`. Lưu ảnh/video từ lần chạy thật vào hồ sơ nộp.

## Điều kiện được phép đóng gói nộp

Chỉ đóng gói bản cuối sau khi backend Release build và tests pass, migration được review chỉ chạm các bảng/index/FK Contact Request, Docker database rỗng chạy health thành công, và build frontend không còn dừng ở `/admin/orders` hoặc các route baseline khác.

Thư mục source hiện tại là snapshot không chứa metadata `.git`; vì vậy không thể kiểm tra `git diff --check`, branch hoặc tạo PR từ sandbox này. Trước khi nộp, hãy copy source vào clone GitHub thật của nhóm, chạy lại kiểm tra Git và thực hiện commit/PR tại clone đó.
