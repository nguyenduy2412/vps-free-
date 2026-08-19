# Audit phạm vi file — PR Contact TV3 thay thế

## Kết luận kiểm kê

Archive TV3-only đã xác minh có **40 file**. Toàn bộ source code trong archive thuộc module Contact Request, test Contact, UI Contact, script/compose override Contact hoặc tài liệu TV3. Archive không chứa News, Landing, Customer, Affiliate, Order, `AuthSecurityExtensions.cs`, `site-header.tsx`, `admin-nav.ts`, migration Contact thủ công hay full-file replacement của các file shared.

> Đây là audit của source contribution. Trước khi mở PR thật, bắt buộc chạy `git diff --name-only origin/dev...HEAD` trên clone `dev` để xác nhận chính xác diff branch.

## File module được phép đưa vào PR

| Nhóm | File |
|---|---|
| Domain | `src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs`, `src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs` |
| Application | Toàn bộ `src/CloudServiceStore.Application/ContactRequests/` (contracts, abstractions, service) |
| Infrastructure | `Persistence/Configurations/ContactRequestConfigurations.cs`, `Persistence/ContactRequestRepository.cs` |
| Web API | `Controllers/ContactRequestsController.cs`, `Security/ContactRequestRateLimitExtensions.cs` |
| Public/Admin UI | `frontend/src/components/contact-public-client.tsx`, `frontend/src/components/admin-contact-requests-client.tsx`, `frontend/src/app/contact/page.tsx`, `frontend/src/app/admin/contact-requests/page.tsx` |
| Test | Bốn `ContactRequest*.cs` trong Application/Integration Tests và hai Postman files TV3 |
| Deploy/script | `docker-compose.contact-empty.yml`, `Build-And-AddContactRequestMigration.ps1`, `Run-ContactRequestEmptyDatabase.ps1` |
| Docs | File `docs/CONTACT_*` và `docs/TV3_*` liên quan Contact/evidence/PR/demo/migration |
| Migration | **Chỉ migration mới được sinh từ `dev` thật** sau khi build/test; không copy migration archive cũ. |

## Shared file chỉ được merge theo hunk

| File | Hunk TV3 duy nhất được phép |
|---|---|
| `CloudServiceStoreDbContext.cs` | Hai `DbSet` Contact. |
| `DependencyInjection.cs` | One using Contact + hai scoped registrations. |
| `Program.cs` | `AddContactRequestRateLimiting` và policy `ManageContactRequests`; local seed chỉ khi nhóm duyệt. |
| `appsettings.json` | `RateLimiting:ContactPermitLimit`; local seed flag chỉ khi nhóm duyệt. |
| `frontend/src/lib/api.ts` | Contact types/query/API module; status endpoint là **POST**. |
| `docker-compose.yml` | Một seed env flag tùy chọn; không đổi image, volume, port hay migrator. |

Xem nội dung copy-paste và mốc neo tại `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`.

## File bị cấm trong PR Contact nếu không có phê duyệt riêng

```text
src/CloudServiceStore.WebApi/Security/AuthSecurityExtensions.cs
frontend/src/components/site-header.tsx
frontend/src/components/admin/admin-nav.ts
src/**/News/**
src/**/Landing/**
src/**/Auth/**
src/**/Orders/**
src/**/Affiliates/**
frontend/src/app/news/**
frontend/src/app/admin/news/**
frontend/src/app/admin/landing/**
```

Không thêm migration archive cũ, snapshot tự chép, file `.env`, secret, `node_modules`, `.next`, `bin` hoặc `obj`.

## Lệnh kiểm tra trước `git add`

```bash
git diff --check
git diff --name-only origin/dev...HEAD | sort
git diff --name-only origin/dev...HEAD | grep -E \
  'AuthSecurityExtensions|site-header|admin-nav|/News/|/Landing/|/Orders/|/Affiliates/' \
  && echo "STOP: có file ngoài phạm vi" || echo "PASS: không thấy file ngoài phạm vi"
```

Review riêng shared files:

```bash
git diff origin/dev...HEAD -- \
  src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs \
  src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/appsettings.json \
  frontend/src/lib/api.ts docker-compose.yml
```
