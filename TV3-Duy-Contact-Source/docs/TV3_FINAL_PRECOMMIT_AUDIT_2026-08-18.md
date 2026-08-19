# Audit cuối trước commit TV3 — Contact Request

## Phạm vi đối chiếu

Audit này đối chiếu module `feature/contact-request-management` của TV3/Duy với **Bảng phân công công việc** và **Quy ước kỹ thuật dùng chung** được cung cấp ngày 18/08/2026. Chỉ review source Contact TV3, shared hunk có liên quan và evidence trực tiếp; không sửa module TV1/TV2/TV4 để tránh vi phạm phân công.

## Kết quả source TV3

| Nhóm kiểm tra | Kết quả | Evidence |
|---|---|---|
| Scope | Pass tĩnh: Domain/Application/Infrastructure/WebApi/frontend/test/script Contact tách riêng; không có News/Landing/Customer/Affiliate/Order/Auth/Header/Nav replacement. | Manifest 27 source file và archive scan. |
| Clean Architecture | Pass tĩnh: controller gọi `IContactRequestService`; EF query nằm ở `ContactRequestRepository`; DTO records tách EF entity; workflow/audit/validation ở `ContactRequestService`. | Source Contact. |
| Contract API | Pass: base `/api/v1`; public `POST /contact-requests`; policy `ManageContactRequests` cho list/detail/status; status giữ `POST /{id}/status` theo contract TV3 hiện hành. | Controller + API client/E2E/Postman. |
| ProblemDetails/OpenAPI | Pass: GetById qua `Execute`; `UnauthorizedAccessException → 401`; public create khai báo `429`; protected actions khai báo `401/403`. | Controller tests reflection + policy tests. |
| Validation/rate limit | Pass tĩnh: DTO `[param:]` annotations và service defense-in-depth; IP named rate limit đọc `RateLimiting:ContactPermitLimit`. | Contracts/service/rate limiter. |
| Secret scan | Pass tĩnh: không thấy connection string/password/JWT key thật trong TV3 source/script candidates. | Static grep audit. |
| Build/test | Pass thật: Release build 0 warning/0 error; Domain 22/22, Application 121/121, Integration 80/80; tổng **223/223** pass; 3 Cobertura artifacts. | `dotnet-build-final-precommit-audit.log`, `dotnet-test-coverage-final-precommit-audit.log`. |
| Format TV3 | Pass thật: `dotnet format` verify Application Contact và ContactRequestsController không báo lỗi. | Scoped format logs. |
| Contact coverage | Pass thật: meaningful Contact line coverage 98.49%; Service/Repository 100%. | Coverage analysis 223. |

## Sai sót đã phát hiện và đã xử lý

| Mục | Cách xử lý |
|---|---|
| GetById bypass error wrapper | Đã đổi sang `Execute(...)`, giữ 404 null result và map exceptions chung. |
| Missing actor claim có thể thành 500 | Đã catch `UnauthorizedAccessException` trong `Execute` và trả 401; có test. |
| Swagger thiếu 429 public rate limit | Đã khai báo `ProducesResponseType` 429; có reflection test. |
| Swagger thiếu 401/403 protected Contact | Đã khai báo ở list/detail/status; có reflection test 3 actions. |
| Service/Repository dưới 90% | Đã thêm test history/detail, validation boundaries, date range và repository Include; hai file hiện 100%. |
| Whitespace `CanTransition` | Đã format theo `dotnet format`. |

## Blocker/evidence chưa thể đóng trong snapshot

| Mức | Hạng mục | Lý do và hành động bắt buộc |
|---|---|---|
| P0 | Git diff thật vs `dev` | Snapshot không có `.git`/`origin/dev`; chưa tạo patch diff thật. Chạy `Export-TV3ContactPreCommitPatch.ps1` trên official Git clone để sinh patch/files/summary. |
| P0 | Contact-only EF migration | Không được tự viết migration. Sinh từ latest `dev`, review `Up/Down`, rồi chạy migration safety script. |
| P0 | Docker database rỗng | Sandbox không có Docker daemon; cần evidence SQL healthy, migrator exit 0, API health 200 từ máy có Docker Desktop. |
| P0 | GitHub CI/review/PR | URL repository cũ không truy cập được và snapshot không có remote; TV3 cần push branch thật, mở PR `feature/contact-request-management → dev`, CI xanh và TV2 review thật. |
| External | Solution-wide `dotnet format` | Dừng tại `src/CloudServiceStore.WebApi/Controllers/ReportingController.cs:52` whitespace. Đây là module Reporting TV4; TV3 không sửa để tránh lấn phạm vi. Chủ file/nhóm trưởng cần xử lý trước release gate toàn solution. |

## Cổng commit cuối TV3

1. Trên official clone, `git fetch origin --prune`, checkout `feature/contact-request-management`, merge/rebase latest `dev` theo guide.
2. Sinh migration Contact-only, chạy `Test-ContactRequestMigrationSafety.ps1`, và lưu report.
3. Chạy `Export-TV3ContactPreCommitPatch.ps1`; nếu summary có `outside-tv3-scope`, dừng và bỏ file đó khỏi commit; shared file phải review hunk.
4. Chạy `Test-ContactRequestPrePr.ps1`, Docker empty DB script và frontend Contact E2E trên môi trường thật.
5. Stage source Contact + migration Contact-only + tests + docs/evidence cần thiết; không stage `.env`, secrets, build artifacts, module khác hoặc patch artifact trừ khi nhóm quy định lưu evidence.
6. Commit với identity GitHub của TV3 và conventional message đúng phạm vi; push branch, mở PR vào `dev`, request TV2 review. Không push `main` hoặc tuyên bố Docker/CI/review pass khi chưa có evidence thật.
