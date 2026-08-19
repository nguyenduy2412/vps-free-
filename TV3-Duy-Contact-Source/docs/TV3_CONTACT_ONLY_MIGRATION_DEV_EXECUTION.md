# Thực thi migration Contact-only trên clone `dev` thật

> Migration là gate bắt buộc nhưng **không được sinh từ ZIP/snapshot**. Chỉ tạo sau khi source Contact và shared hunk đã nằm trên branch local lấy từ `dev` mới nhất của repository chính thức.

## 1. Điều kiện đầu vào

| Điều kiện | Cách xác nhận | Nếu không đạt |
|---|---|---|
| Git worktree chính thức | `git rev-parse --is-inside-work-tree`, `git remote -v` | Dừng; không `git init` ZIP. |
| `dev` mới nhất | `git fetch --prune origin`, `git switch dev`, `git pull --ff-only origin dev` | Dừng và xử lý remote/conflict trước. |
| Branch TV3 local | `git switch -c feature/contact-request-management` | Không làm trên `main`, không làm trực tiếp trên `dev`. |
| Working tree sạch trước copy | `git status --short` không output | Commit/stash công việc ngoài Contact trước. |
| .NET SDK + EF CLI | `dotnet --info`, `dotnet ef --version` | Cài đúng SDK/tool theo repo trước. |

## 2. Áp dụng source trước migration

Copy non-shared Contact từ package TV3 hiện tại, sau đó merge **từng hunk** theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Shared hunk phải gồm DbContext, DI, `Program.cs`, appsettings, `api.ts`, docker-compose và `admin-nav.ts` Contact-only. Riêng navigation chỉ thêm `IconMessageCircle` + item `/admin/contact-requests` Admin/Editor; không thay nguyên file.

Chạy audit scope trước build:

```bash
git diff --check
git diff -- \
  src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs \
  src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/appsettings.json \
  frontend/src/lib/api.ts \
  frontend/src/components/admin/admin-nav.ts \
  docker-compose.yml
```

Nếu diff có Auth, JWT, refresh token, News, Landing, Order, Affiliate, Dashboard, port/image/volume Docker hoặc nav item khác, dừng và loại thay đổi ngoài scope trước migration.

## 3. Build/test bắt buộc trước EF

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build --no-restore
```

PowerShell có thể dùng script bundle sau khi đã merge hunk:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Build-And-AddContactRequestMigration.ps1
```

Script tự restore/build/test, kiểm tra `dotnet ef` và chỉ sau đó gọi EF. Không dùng `-SkipTests` cho evidence nộp/PR trừ khi đang chẩn đoán nhanh rồi chạy lại đầy đủ sau đó.

## 4. Tạo migration bằng CLI

Nếu chạy trực tiếp từ root clone:

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj \
  --startup-project src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj \
  --output-dir Persistence/Migrations
```

Sau lệnh này, **không chạy `database update` ngay**. Lưu tên/timestamp migration, sau đó review diff trước.

## 5. Review migration bắt buộc

```bash
git status --short
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

- [ ] `Up()` chỉ có `ContactRequests`, `ContactRequestStatusHistories`, Contact indexes và Contact FK.
- [ ] `Down()` chỉ đảo chính xác các thay đổi Contact vừa tạo.
- [ ] Không có `CreateTable`, `DropTable` hoặc `AlterColumn` cho AppUsers, News, Orders, Promotions, Affiliate hay module khác.
- [ ] FK AppUser (nếu có) dùng `Restrict`; Contact history tới Contact dùng `Cascade` theo configuration.
- [ ] Index khớp query: date desc, status/date, email/date duplicate window, history contact/date.
- [ ] Không có seed, password, token, connection string, LocalDB path hoặc SQL thủ công bất thường.

Nếu một ô fail: xóa **migration vừa sinh**, không sửa tay để che model drift; quay lại kiểm tra baseline `dev`, shared hunk/DbContext/configuration, rồi sinh lại khi model đúng.

## 6. Chỉ sau review: database disposable và Docker rỗng

Migration đã review mới được test bằng DB disposable/volume Contact-empty. Đặt `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` trỏ đến Initial Catalog có prefix `ContactRequestIntegration_` và đặt `REQUIRE_CONTACT_SQLSERVER_TESTS=true` để pipeline không được phép skip SQL Server test:

```powershell
$env:CONTACT_TEST_SQLSERVER_CONNECTION_STRING = "Server=localhost,1433;Database=ContactRequestIntegration_TV3;User Id=sa;Password=<secret>;TrustServerCertificate=True"
$env:REQUIRE_CONTACT_SQLSERVER_TESTS = "true"
dotnet test tests/CloudServiceStore.Integration.Tests/CloudServiceStore.Integration.Tests.csproj --filter "FullyQualifiedName~ContactRequestSqlServerMigrationTests"
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

Kỳ vọng: migration test dùng connection string guarded `ContactRequestIntegration_`, SQL healthy, migrator exit 0, API running và health 200. Không dùng DB dev/staging/prod làm disposable DB và không chạy `down -v` trên volume nhóm.

## 7. Evidence sau migration

Lưu SHA `dev` baseline, `git diff` migration, output build/test, log migration-safety, compose `ps`, migrator log và health output. Loại password/JWT/token/connection string khỏi evidence. Chỉ sau tất cả gate, mới tính tới commit/PR theo quyền GitHub mà user cho phép.
