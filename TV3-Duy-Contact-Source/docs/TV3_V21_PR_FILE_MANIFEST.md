# Manifest phạm vi PR — Contact Request TV3/Duy

## Kết quả kiểm kê

Phạm vi source Contact trong manifest này có **22 file code/UI/E2E/test/script**. Quét tên file không phát hiện News, Landing, Customer, Affiliate, Order, Auth, Header/Nav, `AuthSecurityExtensions.cs`, full-file shared replacement, `.env`, `node_modules`, `.next`, `bin`, `obj`, certificate/key hoặc migration Contact thủ công.

> Archive là nơi giữ hồ sơ/evidence. PR vào `dev` chỉ nên chứa code Contact, config Contact, shared hunk đã review, migration mới sinh từ `dev` và tài liệu tối thiểu; không commit raw log/coverage nếu nhóm không yêu cầu.

## A. File code Contact được phép trong PR

```text
src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs
src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs

src/CloudServiceStore.Application/ContactRequests/ContactRequestAbstractions.cs
src/CloudServiceStore.Application/ContactRequests/ContactRequestContracts.cs
src/CloudServiceStore.Application/ContactRequests/ContactRequestService.cs

src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs
src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs

src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs
src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs

frontend/src/app/contact/page.tsx
frontend/src/app/admin/contact-requests/page.tsx
frontend/src/components/contact-public-client.tsx
frontend/src/components/admin-contact-requests-client.tsx
frontend/src/lib/contact-request-status.ts
frontend/playwright.config.ts
frontend/e2e/contact-public.spec.ts

tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs
tests/CloudServiceStore.Integration.Tests/ContactRequestsControllerTests.cs
tests/CloudServiceStore.Integration.Tests/ContactRequestRateLimitIntegrationTests.cs
tests/CloudServiceStore.Integration.Tests/ContactRequestSqlServerMigrationTests.cs
tests/CloudServiceStore.Integration.Tests/ContactRequestQueryApiTests.cs

scripts/Build-And-AddContactRequestMigration.ps1
scripts/Run-ContactRequestEmptyDatabase.ps1
scripts/Start-ContactRequestDemo.ps1
scripts/Test-ContactRequestPrePr.ps1
scripts/Test-ContactRequestMigrationSafety.ps1
scripts/Export-TV3ContactPreCommitPatch.ps1
```

## B. Config/support file Contact được phép khi cần

```text
frontend/package.json
frontend/package-lock.json
docker-compose.contact-empty.yml
tests/postman/CloudServiceStore_TV3.postman_collection.json
tests/postman/CloudServiceStore_TV3_Local.postman_environment.json
```

`package.json`/lock chỉ thêm Playwright E2E script và `@playwright/test`. `docker-compose.contact-empty.yml` là override database rỗng riêng của Contact; không thay file compose chính.

## C. Shared file chỉ merge hunk, không copy toàn file

| Shared file | Hunk Contact được phép |
|---|---|
| `CloudServiceStoreDbContext.cs` | 2 DbSet Contact và ApplyConfiguration Contact. |
| `DependencyInjection.cs` | repository/service registrations Contact. |
| `Program.cs` | named Contact rate limiter, policy `ManageContactRequests`; không thêm auto-migration. |
| `appsettings.json` | key rate limit Contact; demo seed chỉ khi nhóm chấp thuận. |
| `frontend/src/lib/api.ts` | Contact types/API, status endpoint **POST**. |
| `docker-compose.yml` | seed flag tùy chọn và hunk healthcheck `SELECT 1` để tránh chặn migrator ở database mới; không thay service/image/volume/port/command migrator. |

Nội dung và mốc neo chính xác: `docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`.

## D. Migration cần tạo trên branch thật

Sau khi code/hunk compile trên clone `dev`, migration mới được phép có:

```text
src/CloudServiceStore.Infrastructure/Persistence/Migrations/
  <timestamp>_AddContactRequestManagement.cs
  <timestamp>_AddContactRequestManagement.Designer.cs
  CloudServiceStoreDbContextModelSnapshot.cs (thay đổi model Contact)
```

Không copy migration Contact cũ trong archive. Review `Up()`/`Down()` theo `TV3_CONTACT_EF_MIGRATION_RUNBOOK.md` trước commit.

## E. Tài liệu nên có trong PR

```text
docs/CONTACT_REQUEST_README.md
docs/CONTACT_REQUEST_PROGRESS_REPORT.md
docs/CONTACT_REQUEST_PROGRESS_REPORT.pdf
docs/CONTACT_REQUEST_DEMO_SCRIPT.md
docs/TV3_PR_BODY_READY_TO_COPY.md
docs/TV3_CONTACT_EF_MIGRATION_RUNBOOK.md
docs/TV3_REPLACEMENT_PR_FILE_SCOPE_AUDIT.md
docs/TV3_FINAL_EXECUTION_RUNBOOK.md
```

Các audit lịch sử, Word tổng hợp, quy ước/bảng phân công tham chiếu, raw logs và coverage XML nên giữ trong archive/hồ sơ bàn giao. Chỉ commit chúng nếu team lead yêu cầu evidence trực tiếp trong repository.

## F. Cấm xuất hiện trong PR Contact

```text
src/**/News/**
src/**/Landing/**
src/**/Orders/**
src/**/Affiliates/**
src/**/Auth/**
frontend/src/app/news/**
frontend/src/app/admin/news/**
frontend/src/app/admin/landing/**
src/CloudServiceStore.WebApi/Security/AuthSecurityExtensions.cs
frontend/src/components/site-header.tsx
frontend/src/components/admin/admin-nav.ts
.env, password/token/key thật, node_modules, .next, bin, obj
```

## Lệnh cổng kiểm tra trước push

```bash
git diff --check
git diff --name-only origin/dev...HEAD | sort
git diff --name-only origin/dev...HEAD | grep -E \
  'AuthSecurityExtensions|site-header|admin-nav|/News/|/Landing/|/Customer|/Orders/|/Affiliates/|/Auth/' \
  && echo "STOP: file ngoài phạm vi" || echo "PASS: chỉ scope Contact"
```
