# Manifest nguồn chi tiết — TV3/Duy Contact Request

> Manifest này mô tả **phạm vi source TV3-only** để người nhận biết chính xác file nào được copy vào clone `dev` và file nào chỉ được merge hunk. Số dòng là số liệu tĩnh của package source hiện hành trước khi migration EF thật được sinh từ `dev`.

## 1. Tổng quan package source

| Nhóm | Số file | Số dòng mã hiển thị | Mục tiêu |
|---|---:|---:|---|
| C# Domain/Application/Infrastructure/Web API | 9 | 840 | Nghiệp vụ Contact, persistence, HTTP API và rate limit. |
| C# test Contact | 5 | 881 | Unit, controller, query/authorization, rate limit, migration safety. |
| Frontend TypeScript/TSX/E2E | 8 | 508 | Form public, admin UI, UI workflow metadata, E2E và responsive evidence fixture. |
| Script vận hành | 11 | — | Copy local, migration, DB rỗng, pre-PR, smoke, CORS, E2E low-resource. |
| Docs | 60+ | — | Integration hunk, migration, staging, PR, demo, DoD và handover. |

Không có migration EF Contact được đóng gói. Migration hợp lệ phải sinh từ `dev` thật sau khi hunk đã merge và build/test đạt.

## 2. C# source TV3-only

| File | Dòng | Vai trò | Copy hay merge hunk |
|---|---:|---|---|
| `src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs` | 10 | Enum `Pending`, `Contacted`, `Approved`, `Rejected`, `Cancelled`. | Copy. |
| `src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs` | 36 | Aggregate Contact và history, kế thừa convention audit. | Copy. |
| `src/CloudServiceStore.Application/ContactRequests/ContactRequestAbstractions.cs` | 39 | Repository abstractions và exceptions module Contact. | Copy. |
| `src/CloudServiceStore.Application/ContactRequests/ContactRequestContracts.cs` | 88 | Request/DTO/query/page contracts tách Entity. | Copy. |
| `src/CloudServiceStore.Application/ContactRequests/ContactRequestService.cs` | 327 | Validation, duplicate 24h, workflow, audit metadata, mapping. | Copy. |
| `src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs` | 75 | EF table/field/index/FK SQL Server. | Copy. |
| `src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs` | 91 | Filter/page/detail/history/duplicate/audit persistence. | Copy. |
| `src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs` | 136 | Public create; Admin/Editor list/detail/status; ProblemDetails. | Copy. |
| `src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs` | 38 | Named `contact-requests` policy theo IP. | Copy. |

## 3. C# tests TV3-only

| File | Dòng | Trọng tâm coverage |
|---|---:|---|
| `tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs` | 393 | Boundary validation, normalized input, duplicate, workflow, history, audit privacy, paging guard. |
| `tests/CloudServiceStore.Integration.Tests/ContactRequestsControllerTests.cs` | 249 | HTTP result mapping 201/400/401/404/409/429, OpenAPI metadata, actor/IP. |
| `tests/CloudServiceStore.Integration.Tests/ContactRequestQueryApiTests.cs` | 159 | Pagination, filter status/date, detail history, Admin/Editor/Customer/anonymous access. |
| `tests/CloudServiceStore.Integration.Tests/ContactRequestRateLimitIntegrationTests.cs` | 39 | HTTP 429 named policy Contact. |
| `tests/CloudServiceStore.Integration.Tests/ContactRequestSqlServerMigrationTests.cs` | 41 | Schema Contact trên SQL Server disposable guarded DB. |

## 4. Frontend TV3-only

| File | Dòng | Vai trò |
|---|---:|---|
| `frontend/src/app/contact/page.tsx` | 2 | Route public `/contact`. |
| `frontend/src/components/contact-public-client.tsx` | 47 | Form public loading/error/success/request ID/disabled submit. |
| `frontend/src/app/admin/contact-requests/page.tsx` | 2 | Route quản trị `/admin/contact-requests`. |
| `frontend/src/components/admin-contact-requests-client.tsx` | 131 | List/filter/paging/detail/history/status action. |
| `frontend/src/lib/contact-request-status.ts` | 32 | UX metadata/transition hints; backend vẫn authoritative. |
| `frontend/e2e/contact-public.spec.ts` | 182 | 9 public form cases × 3 viewports. |
| `frontend/e2e/contact-responsive-evidence.spec.ts` | 76 | Evidence-only UI mock fixture desktop/360px; skip mặc định. |
| `frontend/playwright.config.ts` | 36 | Desktop, iPhone và 360×800 projects. |

`frontend/src/lib/api.ts` và `frontend/src/components/admin/admin-nav.ts` là **shared files**. Không có full replacement trong package; merge Contact hunk từ guide. Hunk nav tối thiểu cần để route guard Admin/Editor không redirect khỏi Contact.

## 5. Script vận hành

| Script | Dùng khi | Không chứng minh thay cho |
|---|---|---|
| `Apply-ContactV45ToLocalDev.sh` | Copy non-shared Contact/hướng dẫn merge hunk local. | Git branch/PR thật. |
| `Build-And-AddContactRequestMigration.ps1` | Restore/build/test và gọi EF migration trên clone dev thật. | Review migration. |
| `Run-ContactRequestEmptyDatabase.ps1` | Docker DB rỗng Contact. | Staging/production. |
| `Test-ContactRequestMigrationSafety.ps1` | SQL Server disposable guarded. | Migration review `Up/Down`. |
| `Test-ContactRequestPrePr.ps1` | Pre-PR validation có coverage. | CI PR mới. |
| `Test-ContactRequestStagingSmoke.ps1` | Smoke staging sau deploy. | Docker local/CI. |
| `Check-StagingCorsCompose.sh` | Validate origin/env + Compose staging render. | Start container thật. |
| `Run-ContactE2ELowResource.sh` | E2E tuần tự một worker. | E2E API Docker thật. |
| `Check-ContactControllerArchitecture.sh` | Guard controller service-only/static POST contract. | Review kiến trúc trên dev. |
| `Start-ContactRequestDemo.ps1` | Chuẩn bị demo local. | Evidence Docker/CI. |
| `Export-TV3ContactPreCommitPatch.ps1` | Export patch local để review. | Commit/push thật. |

## 6. File tuyệt đối không copy nguyên

| Shared file | Chỉ được merge hunk Contact |
|---|---|
| `CloudServiceStoreDbContext.cs` | Hai DbSet Contact. |
| `DependencyInjection.cs` | Repository/service registrations. |
| `Program.cs` | Policy, registration rate limit, optional local seed guard. |
| `appsettings.json` | Contact permit limit và optional seed keys. |
| `frontend/src/lib/api.ts` | Contact types/query/API module. |
| `frontend/src/components/admin/admin-nav.ts` | `IconMessageCircle` + Contact menu Admin/Editor. |
| `docker-compose.yml` | SQL healthcheck/migrator hunk Contact-only. |

Xem `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md` để merge theo mốc neo. Nếu diff có Auth, News, Landing, Customer, Order, Affiliate hoặc thay toàn file shared, dừng và loại phần ngoài scope.
