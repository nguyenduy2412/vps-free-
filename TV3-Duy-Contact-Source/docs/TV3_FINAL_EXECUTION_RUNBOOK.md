# Runbook thực thi cuối — Contact Request TV3 trên clone `dev` thật

> Mục tiêu của runbook này là đưa source Contact TV3 vào branch đúng, sinh migration an toàn và tạo evidence thật trước PR. Không thay thế file shared nguyên bản, không dùng migration archive cũ và không commit secret.

## Bước 0 — Chuẩn bị clone và branch

Mở terminal tại repository chính thức có `CloudServiceStore.sln`.

```bash
git fetch origin --prune
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management
```

Xác nhận working tree sạch trước khi copy:

```bash
git status --short
```

Nếu có thay đổi của người khác, dừng lại để stash/commit đúng chủ sở hữu; không trộn thay đổi đó vào branch Contact.

## Bước 1 — Copy source Contact và merge hunk shared

Từ archive TV3, copy **27 file source/UI/E2E/test/script** thuộc Contact. Các file chính nằm tại:

| Khu vực | Nội dung được copy |
|---|---|
| Domain | `ContactRequestEntities.cs`, `ContactRequestStatus.cs` |
| Application | Toàn bộ `Application/ContactRequests/` |
| Infrastructure | Contact configuration và repository |
| Web API | Contact controller và named Contact rate limiter |
| Frontend | Public form, Admin Contact, status metadata UI, route `/contact`, route `/admin/contact-requests`, Playwright config/E2E Contact |
| Tests | Năm test file Contact backend, gồm query pagination/status filter/authorization |
| Script | `Build-And-AddContactRequestMigration.ps1`, `Run-ContactRequestEmptyDatabase.ps1`, `Start-ContactRequestDemo.ps1`, `Test-ContactRequestPrePr.ps1`, `Test-ContactRequestMigrationSafety.ps1`, `Export-TV3ContactPreCommitPatch.ps1`, `Test-ContactRequestStagingSmoke.ps1` |

Không copy module/file ngoài phạm vi:

```text
News, Landing, Customer, Affiliate, Order, Auth,
AuthSecurityExtensions.cs, site-header.tsx, admin-nav.ts
```

Với shared files, mở `docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md` và chỉ merge hunk Contact vào các file sau:

```text
CloudServiceStoreDbContext.cs
DependencyInjection.cs
Program.cs
appsettings.json
frontend/src/lib/api.ts
docker-compose.yml
```

Không copy toàn file shared từ archive. Xem diff ngay sau merge:

```bash
git diff --check
git diff --name-only origin/dev...HEAD | sort
git diff --name-only origin/dev...HEAD | grep -E \
  'AuthSecurityExtensions|site-header|admin-nav|/News/|/Landing/|/Customer|/Orders/|/Affiliates/' \
  && echo "STOP: có file ngoài phạm vi" || echo "PASS: scope Contact"
```

## Bước 2 — Restore, build, test và sinh migration từ `dev`

### Phương án PowerShell (khuyến nghị)

Script đã build/test trước rồi mới tạo migration:

```powershell
pwsh ./scripts/Build-And-AddContactRequestMigration.ps1
```

Nếu môi trường thiếu `pwsh`, dùng PowerShell trên Windows:

```powershell
.\scripts\Build-And-AddContactRequestMigration.ps1
```

### Phương án CLI trực tiếp

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults

dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure \
  --startup-project src/CloudServiceStore.WebApi \
  --output-dir Persistence/Migrations
```

PowerShell dùng backtick thay cho dấu `\`:

```powershell
dotnet ef migrations add AddContactRequestManagement `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi `
  --output-dir Persistence/Migrations
```

### Cổng review migration bắt buộc

Review tất cả file vừa sinh trong `src/CloudServiceStore.Infrastructure/Persistence/Migrations/`.

`Up()` chỉ được tạo `ContactRequests`, `ContactRequestStatusHistories`, index Contact và FK Contact. `Down()` chỉ được drop hai bảng Contact theo thứ tự an toàn. Không được tạo/drop/alter bảng Auth, News, Landing, Order, Promotion, Affiliate, Catalog hoặc Dashboard.

```bash
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

Nếu diff có schema ngoài Contact, xóa migration vừa sinh bằng `dotnet ef migrations remove`, kiểm tra lại baseline/hunk DbContext/configuration rồi sinh lại. Không chỉnh migration bằng tay để che model drift.

Sau review thủ công, chạy safety gate trên real Git clone. Chỉ thêm `-RunSqlServerApply` khi `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` trỏ tới database disposable có tên `ContactRequestIntegration_*`:

```powershell
.\scripts\Test-ContactRequestMigrationSafety.ps1
# hoặc với SQL Server disposable thật:
.\scripts\Test-ContactRequestMigrationSafety.ps1 -RunSqlServerApply
```

Script yêu cầu đúng một migration Contact mới so với `origin/dev`, chặn raw SQL/table operation ngoài Contact, cho phép FK Contact tới `AppUsers`, kiểm tra EF list và tạo `MIGRATION_SAFETY_CONTACT_REPORT.md` để đính kèm PR.

### Cổng pre-PR sau migration

Sau khi migration Contact-only đã được review, chạy script pre-PR trên đúng feature branch để tạo report coverage gắn với commit sắp push:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Test-ContactRequestPrePr.ps1
Get-Content .\artifacts\pre-pr-contact\PRE_PR_CONTACT_REPORT.md
```

Script yêu cầu Git clone thật, kiểm tra `git diff --check`, build/test/coverage, targeted Contact query test và frontend lint/build. Nếu merge conflict với `dev`, xử lý theo `TV3_GIT_MERGE_CONFLICT_GUIDE.md` rồi chạy lại script; evidence trước SHA merge conflict không còn đủ.

## Bước 3 — Docker database rỗng và evidence

Đặt các biến môi trường local như `MSSQL_SA_PASSWORD`, `SEED_ADMIN_PASSWORD`, `JWT_SIGNING_KEY`; không commit `.env` hoặc password vào Git.

Chạy script override database rỗng (script tự khởi động Compose, chờ migrator exit `0` và kiểm tra health):

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

Điều kiện đạt:

| Hạng mục | Evidence cần lưu |
|---|---|
| SQL Server | Healthy. |
| Migrator | Exit code `0`; log không lỗi EF migration. |
| API | Container running và `GET /health` trả HTTP `200`. |
| Migration | Hai bảng Contact, history/index/FK Contact có trong database rỗng. |

Sau Docker, chạy E2E Contact:

```bash
cd frontend
npm ci
npm run lint
env -u NODE_ENV npm run build
npm run test:e2e:contact
```

E2E đã có test success, `ProblemDetails` error và no-horizontal-overflow tại 360px. Nếu hồ sơ yêu cầu ảnh, lưu screenshot desktop/mobile từ Playwright report hoặc trình duyệt thật.

## Bước 4 — Commit, PR và cross-review TV2

Trước commit:

```bash
git status --short
git diff --check
git diff --name-only origin/dev...HEAD | sort
```

Commit chỉ source Contact, shared hunk đã review và migration Contact-only. Không commit `.env`, secret, `node_modules`, `.next`, `bin`, `obj` hay coverage raw ngoài evidence đã được nhóm chấp nhận.

Tạo PR `feature/contact-request-management` → `dev`, copy nội dung ở `docs/TV3_PR_DESCRIPTION_DETAILED.md`, kèm screenshot/log migration Docker, `PRE_PR_CONTACT_REPORT.md`, evidence test/E2E và kiểm tra responsive. Nếu PR bị conflict/behind `dev`, làm theo `docs/TV3_GIT_MERGE_CONFLICT_GUIDE.md`; không giải quyết bằng ghi đè toàn file shared. Gửi tin nhắn tại `docs/TV3_TEAM_LEAD_MESSAGE_READY_TO_SEND.md` để nhờ TV2 review theo phân công.

Reviewer TV2 cần nhận xét thực chất về Clean Architecture, policy Admin/Editor, workflow, duplicate 24 giờ, rate limit/audit, migration Contact-only, test/E2E và shared-file diff. Tác giả tự sửa feedback trên chính branch. Chỉ merge khi CI GitHub xanh, migration/Docker evidence đạt và review không còn blocking.

Sau khi PR hợp lệ merge vào `dev`, không push code thành viên trực tiếp vào `main`. Nhóm trưởng thực hiện release gate `dev` → `main` theo `TV3_FINAL_MERGE_AND_RELEASE_CHECKLIST.md`: chạy lại regression trên HEAD `dev`, kiểm tra migration/database rỗng, CI/review/evidence thật rồi mới mở PR release. Khi cần triển khai release candidate lên staging, dùng `TV3_STAGING_DEPLOYMENT_GUIDE.md`; không dùng compose local demo trực tiếp làm cấu hình staging.
