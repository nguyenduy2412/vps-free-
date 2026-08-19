# Hướng dẫn chạy EF Core migration Contact bằng `dotnet ef`

## Mục tiêu

Sinh migration `AddContactRequestManagement` từ **branch `dev` thật đã tích hợp source Contact**, review để chỉ có thay đổi Contact, sau đó mới update database local/DB rỗng. Không viết migration bằng tay và không copy migration Contact cũ từ archive.

## 1. Chuẩn bị branch và cấu hình local

Mở PowerShell tại thư mục root có `CloudServiceStore.sln`:

```powershell
git fetch origin --prune
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management
```

Áp dụng toàn bộ Contact source và shared hunk theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Chưa chạy `database update` ở bước này.

Kiểm tra SDK/tool:

```powershell
dotnet --version
dotnet tool restore
dotnet ef --version
```

Nếu `dotnet ef` chưa có, dùng local tool manifest trước:

```powershell
dotnet tool restore
dotnet ef --version
```

Chỉ khi repository không có manifest mới cài global tool tương thích:

```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

Thiết lập connection string local **không phải production**. Ví dụ SQL Server local Docker:

```powershell
$env:ConnectionStrings__CloudServiceStore = "Server=localhost,1433;Database=CloudServiceStoreTv3Local;User Id=sa;Password=<MAT_KHAU_LOCAL>;TrustServerCertificate=True"
```

Không commit password, `.env` thật hoặc biến môi trường local.

## 2. Build/test trước khi tạo migration

Migration chỉ được sinh khi model đang compile và test qua:

```powershell
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build
```

Nếu có lỗi, dừng và sửa lỗi trước. Không sinh migration trên model đang lỗi rồi sửa file migration bằng tay.

## 3. Sinh migration

Từ root solution, chạy:

```powershell
dotnet ef migrations add AddContactRequestManagement `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi `
  --output-dir Persistence/Migrations
```

Lệnh phải sinh ba file mới trong `src/CloudServiceStore.Infrastructure/Persistence/Migrations/`:

```text
<timestamp>_AddContactRequestManagement.cs
<timestamp>_AddContactRequestManagement.Designer.cs
CloudServiceStoreDbContextModelSnapshot.cs  (thay đổi model snapshot)
```

## 4. Review migration bắt buộc

Mở file migration mới. `Up()` chỉ được có những nội dung sau:

| Cho phép | Không được phép |
|---|---|
| `CreateTable("ContactRequests")` | Tạo/drop/alter bảng Auth, News, Landing, Order, Promotion, Affiliate, Catalog hoặc Dashboard. |
| `CreateTable("ContactRequestStatusHistories")` | Đổi cột/index của bảng ngoài Contact. |
| FK `ContactRequests.AppUserId -> AppUsers.Id` với `Restrict` | Xóa/sửa `AppUsers`, `Roles`, `RefreshTokens`. |
| FK history -> ContactRequests với `Cascade` | Thay đổi FK module khác. |
| Index Contact: CreatedAt, Status/CreatedAt, AppUserId/CreatedAt, Email/CreatedAt, ContactRequestId/CreatedAt | Index không liên quan Contact. |

`Down()` chỉ được drop `ContactRequestStatusHistories` và `ContactRequests` theo thứ tự an toàn.

Lệnh review:

```powershell
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

Nếu có bảng/cột ngoài Contact: **xóa migration vừa sinh bằng `dotnet ef migrations remove`**, kiểm tra lại branch `dev`, DbContext/configuration/snapshot, rồi sinh lại. Không chỉnh migration bằng tay để che model drift.

```powershell
dotnet ef migrations remove `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi
```

## 5. Tạo script SQL để review (khuyến nghị)

```powershell
dotnet ef migrations script `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi `
  --output .\artifacts\contact-migration.sql
```

Mở `artifacts/contact-migration.sql`; xác nhận SQL chỉ tạo hai bảng Contact, index và FK Contact trước khi update database.

## 6. Update database local/DB rỗng

Sau review, update database local disposable:

```powershell
dotnet ef database update `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi
```

Để kiểm tra database rỗng qua Docker Compose override riêng:

```powershell
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml up --build --detach
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps
```

Kiểm tra `migrator` exit `0`, API `/health` trả `200`, sau đó chạy Postman Contact. Chỉ xóa volume database rỗng riêng khi thực sự muốn reset; không dùng `down --volumes` cho compose dev/prod chung.

## 7. Staging migration vào PR

```powershell
git status --short
git add src/CloudServiceStore.Infrastructure/Persistence/Migrations
git diff --cached -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

Chỉ commit khi diff migration đã được reviewer/owner xác nhận là Contact-only.
