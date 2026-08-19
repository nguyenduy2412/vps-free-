# Xử lý lỗi EF Core migration và Docker database rỗng — Contact TV3

> Chỉ thực hiện các lệnh EF/Docker dưới đây trên **clone Git `dev` thật** và database disposable/volume `Contact-empty`. Không dùng archive source làm baseline, không dùng DB dev dùng chung, staging hoặc production để thử migration.

## 1. Trình tự an toàn trước khi xử lý lỗi

```bash
git fetch --prune origin
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management
git status --short
git diff --check
```

Working tree phải sạch trước khi copy Contact/hunk. Sau merge hunk, phải build/test trước khi tạo migration. Nếu `git status --short` không rỗng hoặc diff có module ngoài Contact, dừng và tách phần ngoài scope trước.

## 2. EF Core migration Contact-only

### Lệnh chuẩn

Chạy từ repository root sau khi source Contact và shared hunk đã merge:

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build --no-restore

dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj \
  --startup-project src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj \
  --output-dir Persistence/Migrations
```

PowerShell tương đương:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Build-And-AddContactRequestMigration.ps1
```

Script PowerShell kiểm tra solution/project paths, restore, Release build, test, `dotnet ef --version`, rồi mới sinh migration. Không dùng `-SkipTests` cho evidence PR; chỉ dùng tạm khi đang chẩn đoán và chạy lại đầy đủ trước review.

### Bảng chẩn đoán EF

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý đúng |
|---|---|---|
| `dotnet: command not found` hoặc SDK không đúng | Chưa cài .NET SDK yêu cầu của repo. | Cài SDK/khởi động terminal mới; kiểm tra `dotnet --info`. Không sửa `.csproj` chỉ để vượt môi trường local. |
| `dotnet ef` không tìm thấy | EF CLI/tool chưa có hoặc PATH sai. | Kiểm tra `dotnet ef --version`; cài/restore tool theo convention repository, rồi chạy lại. |
| Build fail trước EF | Hunk shared chưa merge, contract/type namespace sai hoặc package restore thiếu. | Sửa build trước; không sinh migration từ model đang lỗi. |
| `Unable to create a DbContext` | Startup project/configuration/DI không khởi tạo được design-time. | Dùng đúng hai đường dẫn `.csproj`; kiểm tra DbContext hunk, connection/configuration design-time và build WebApi. Không hard-code LocalDB/password. |
| EF báo không có thay đổi model | DbSet/configuration Contact chưa merge vào shared DbContext/model assembly hoặc baseline không phải dev mới. | Rà `CloudServiceStoreDbContext.cs` và configuration hunk; xác nhận branch từ `dev`; không tạo migration rỗng. |
| Sinh ra nhiều file migration ngoài ý muốn | Working tree có migration khác, model drift hoặc chọn sai baseline. | Xóa **chỉ migration vừa sinh chưa apply**, làm sạch branch/rebase dev rồi sinh lại. |
| Migration chứa News/Order/Affiliate/Auth/Landing/Customer | Model drift hoặc overlay/shared file sai phạm vi. | Dừng ngay; không sửa tay để che diff. Xóa migration vừa sinh, diff `dev...HEAD`, tìm hunk/branch sai rồi sinh lại. |
| Migration có raw `migrationBuilder.Sql` | Migration không theo Contact configuration chuẩn. | Dừng và review configuration; raw SQL không được phép trong Contact migration. |
| `Down()` drop/alter bảng ngoài Contact | Migration baseline sai phạm vi. | Không chạy database update; remove generated migration chưa apply và quay lại baseline/hunk. |

### Review bắt buộc sau khi EF chạy

```bash
git status --short
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

Migration hợp lệ có đúng một migration `.cs` mới so với `origin/dev`; `Up()`/`Down()` chỉ tạo/đảo `ContactRequests`, `ContactRequestStatusHistories`, Contact FK và Contact indexes. Principal FK tới `AppUsers` chỉ được dùng nếu configuration Contact yêu cầu. Không apply database cho đến khi review xong.

Sau review, chạy guard:

```powershell
.\scripts\Test-ContactRequestMigrationSafety.ps1
```

Muốn áp vào SQL Server disposable, đặt `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` chứa tên database bắt đầu `ContactRequestIntegration_`, sau đó chạy:

```powershell
.\scripts\Test-ContactRequestMigrationSafety.ps1 -RunSqlServerApply
```

Guard từ chối connection string không chứa prefix disposable này. Không bypass guard bằng cách đổi tên DB dev/staging.

## 3. Docker database rỗng

### Lệnh chuẩn

Kiểm tra Compose trước, sau đó reset **chỉ** override Contact-empty:

```bash
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml config
```

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
```

Script dùng đúng hai compose file, khởi động detached, chờ tối đa 180 giây cho migrator, bắt buộc exit code `0`, rồi poll `/health` tối đa 60 giây để chờ HTTP `200`.

### Bảng chẩn đoán Docker

| Triệu chứng | Thu thập không lộ secret | Cách xử lý đúng |
|---|---|---|
| `docker`/daemon không chạy | `docker info` | Khởi động Docker Desktop/daemon và kiểm tra quyền user; sandbox không có daemon không phải lỗi source. |
| Compose báo missing variable | `docker compose ... config --no-interpolate` | Đặt biến theo `.env.example`/secret manager; không commit `.env` thật. Với staging dùng preflight CORS riêng. |
| SQL Server unhealthy | `docker compose ... ps -a`, `logs sqlserver` | Kiểm tra `MSSQL_SA_PASSWORD`, EULA, port conflict, RAM/disk; không đổi healthcheck sang check database Contact chưa tồn tại. |
| Migrator không exit trong 180s | `logs migrator`, `logs sqlserver` | Chờ SQL healthy, kiểm tra connection string env/migration diff; không restart API liên tục để che lỗi. |
| Migrator exit khác `0` | `logs --no-color migrator` | Đọc lỗi migration/connection; quay lại review migration Contact-only trước reset lại DB disposable. |
| API không health 200 sau migrator | `logs --no-color api`, `ps -a` | Kiểm tra dependency/config JWT/connection string/routing; không giả định migrator 0 đồng nghĩa API healthy. |
| Port `8080`/`1433` đã dùng | `docker ps`, `netstat`/`ss` theo OS | Dừng container local xung đột hoặc đổi port trong override local được review; không sửa compose shared ngoài hunk. |
| Reset làm mất dữ liệu không mong muốn | Xem chính xác hai file compose trước lệnh | Chỉ dùng `Run-ContactRequestEmptyDatabase.ps1 -Reset`; không dùng `docker compose down -v` chung trên stack nhóm. |

### Evidence Docker tối thiểu

Sau khi pass, lưu ngoài PR raw source: output `ps -a`, migrator exit `0`, health `200`, command SHA branch và smoke API. Che password, JWT, token, connection string. Đây là evidence Docker local; staging/CI vẫn cần gate riêng.

## 4. Smoke sau Docker pass

Chỉ bắt đầu smoke khi SQL healthy, migrator `0`, API health `200`.

| Luồng | Expected |
|---|---|
| Public valid create | `201` và request ID. |
| Public duplicate 24h | `409`. |
| Public invalid email/message | `400 ProblemDetails`. |
| Anonymous management call | `401`. |
| Customer management call | `403`. |
| Editor/Admin list/detail/status hợp lệ | `200`. |
| Reject/Cancel không note | `400`. |
| Terminal reopen | `409`. |
| Vượt Contact permit | `429 ProblemDetails`. |

Xem `TV3_STAGING_POST_DEPLOY_SMOKE_CHECKLIST.md` và `Test-ContactRequestStagingSmoke.ps1` cho sequence đầy đủ. Không tắt rate limit/authorization để làm demo thuận tiện.
