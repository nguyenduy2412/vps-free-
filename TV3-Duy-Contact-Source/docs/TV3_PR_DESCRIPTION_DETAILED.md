# PR Description — TV3/Duy Contact Request Management

> **PR target bắt buộc:** `feature/contact-request-management` → `dev`. Không push trực tiếp vào `main`; `main` là nhánh release do nhóm trưởng quyết định merge sau khi feature trên `dev` đạt DoD.

## Tóm tắt

PR này bổ sung module **Contact Request Management** thuộc phần việc TV3/Duy. Module cho phép khách gửi yêu cầu tư vấn công khai, đồng thời cho phép role **Admin** và **Editor** lọc, phân trang, xem chi tiết/lịch sử và cập nhật trạng thái theo workflow backend kiểm soát.

## Phạm vi thay đổi

| Layer | Thay đổi |
|---|---|
| Domain | `ContactRequest`, `ContactRequestStatusHistory`, enum `Pending/Contacted/Approved/Rejected/Cancelled`. |
| Application | DTO tách entity, validation, duplicate window 24 giờ, workflow transition, audit và `PagedResult`. |
| Infrastructure | EF configuration/index, repository query `AsNoTracking`, search/status/date filter, paging và history detail. |
| Web API | Public POST; Admin/Editor GET list/detail và POST status; `ProblemDetails`, named IP rate limit, policy `ManageContactRequests`. |
| Frontend | `/contact`, `/admin/contact-requests`, shared `contactRequestsApi`, UI status metadata tách khỏi component; backend là workflow authority. |
| Test/tooling | Service/controller/rate-limit/migration tests; API test pagination/status filter/authorization; Playwright E2E; pre-PR coverage script; Postman/curl quick tests. |

## API contract

| Method | Endpoint | Access | Expected use |
|---|---|---|---|
| `POST` | `/api/v1/contact-requests` | Anonymous | Submit Contact; IP rate limit. |
| `GET` | `/api/v1/contact-requests` | Admin/Editor | Search, status filter, pagination. |
| `GET` | `/api/v1/contact-requests/{id}` | Admin/Editor | Detail + status history. |
| `POST` | `/api/v1/contact-requests/{id}/status` | Admin/Editor | Workflow update; rejects invalid transition. |

## Business and security rules

- Public duplicate email is rejected within a 24-hour window.
- Workflow is `Pending → Contacted → Approved`, with `Rejected`/`Cancelled` permitted by backend rules; terminal statuses cannot reopen.
- `Rejected` and `Cancelled` require a note.
- Create/status change add audit metadata without storing full message in audit.
- Public POST is rate-limited per IP; policy response is `429` without replacing global rejection behavior.
- Admin/Editor policy is enforced in API, not only hidden in UI.

## Evidence run locally in sandbox

| Command | Result |
|---|---|
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` | Pass, 0 warning / 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` | Pass: Domain 22/22, Application 121/121, Integration 80/80; total **223/223**. |
| Targeted Contact query/controller tests | Pass: status/date filter, descending pages, detail history, 401/403/200 authorization, OpenAPI 401/403/429 metadata and missing actor `401`. |
| `npm ci`, `npm run lint`, `NODE_ENV=production npm run build` | Pass. |
| `npm run test:e2e:contact` | Pass: 27/27 Contact public-form cases across Desktop/iPhone/360px. |

## Local pre-PR verification to paste with evidence

Run the following from the **real Git clone** on branch `feature/contact-request-management` after pulling/merging the latest `dev` and before pushing the PR update:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Export-TV3ContactPreCommitPatch.ps1
.\scripts\Test-ContactRequestMigrationSafety.ps1
.\scripts\Test-ContactRequestPrePr.ps1
Get-Content .\artifacts\pre-pr-contact\PRE_PR_CONTACT_REPORT.md
```

`Export-TV3ContactPreCommitPatch.ps1` must produce a patch and scope summary against `origin/dev` without files outside TV3 Contact. `Test-ContactRequestMigrationSafety.ps1` is run only after migration Contact-only is generated from latest `dev`. The pre-PR script fails fast on whitespace errors, backend restore/build/test failure, missing Cobertura XML, Contact paging/filter/authorization regression, or frontend install/lint/build failure. Attach or quote generated report paths in the PR; do not manually tick a check until commands pass on the current feature commit.

For Docker evidence, run on a machine with Docker Desktop after migration is generated from latest `dev`:

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
```

Only mark Docker complete when `sqlserver` is healthy, `migrator` is `exited (0)`, API health is `200`, and the actual logs are attached/linked.

## Shared-file / migration safety

- Only merge documented hunk(s) into DbContext, DI, Program, appsettings, frontend API client and Docker Compose; never replace full shared files.
- Healthcheck hunk uses SQL readiness `SELECT 1`, preventing a new database from blocking migrator before EF can create it.
- No manual Contact migration is included. Create migration from latest `dev`, then review `Up()`/`Down()`; it may only create Contact tables, indexes and FKs.
- Run `Test-ContactRequestMigrationSafety.ps1` after generation; attach `MIGRATION_SAFETY_CONTACT_REPORT.md`. With disposable SQL Server configured, rerun using `-RunSqlServerApply`.
- This PR excludes News, Landing, Customer, Affiliate, Order, Auth, Header/Nav, `AuthSecurityExtensions.cs`, real `.env` and build artifacts.

## Pre-merge checklist

- [x] Local backend build/test/coverage evidence is attached.
- [x] Frontend lint/build and Contact E2E evidence are attached.
- [x] Query filter/pagination/authorization test is included.
- [x] Controller 401/403/429 OpenAPI metadata and missing-actor 401 regression are included.
- [ ] Pre-commit patch generated from the official clone against latest `origin/dev`; no outside-TV3 file or unreviewed shared hunk remains.
- [ ] Migration Contact-only is generated and reviewed on latest `dev` clone.
- [ ] Migration safety script passes; disposable SQL Server migration test evidence is attached when environment is available.
- [ ] Docker empty-database run shows SQL healthy, migrator exit 0 and `/health` 200.
- [ ] GitHub Actions for this PR is green.
- [ ] TV2 submits substantive cross-review and approval (architecture, policy, validation, migration and regression).
- [ ] TV3 posts the real PR link to the group for cross-review.

## Update after resolving conflicts with `dev`

If GitHub reports the branch is behind/conflicting, TV3 must resolve on the feature branch following `TV3_GIT_MERGE_CONFLICT_GUIDE.md`, then re-run the local pre-PR script. The PR description must state the commit SHA used for the evidence after conflict resolution; prior evidence from an older feature commit is not sufficient.

## Reviewer focus

1. Confirm shared-file diff contains only Contact hunks.
2. Verify Admin/Editor policy and Anonymous public create behavior.
3. Verify duplicate, note, terminal transition, audit and rate-limit behavior.
4. Review migration scope and database-rỗng evidence.
5. Review new paging/status filter/authorization tests and frontend no-regression evidence.
