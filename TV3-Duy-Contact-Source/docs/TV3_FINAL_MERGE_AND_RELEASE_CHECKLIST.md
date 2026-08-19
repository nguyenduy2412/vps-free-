# Checklist cuối — Feature TV3 → `dev` → release `main`

Checklist này phân biệt rõ ba cổng: **feature readiness**, **PR merge vào `dev`**, và **release do nhóm trưởng quyết định vào `main`**. Không tick mục GitHub/Docker/migration nếu chưa có link/log từ repository hoặc máy chạy thật.

## A. Trước khi TV3 push/update PR

| Điều kiện | Bằng chứng cần lưu |
|---|---|
| Branch đúng | `git branch --show-current` là `feature/contact-request-management`, branch base là `dev`. |
| Identity thật | `git config --get user.name` và `git config --get user.email` là của TV3. |
| Shared-file scope | `git diff origin/dev...HEAD --name-only` không có file module khác; shared file chỉ chứa hunk Contact. |
| Whitespace/build/test/coverage | `Test-ContactRequestPrePr.ps1` pass và sinh `artifacts/pre-pr-contact/PRE_PR_CONTACT_REPORT.md`. |
| Migration | Sinh từ `dev` thật, `Up/Down` chỉ Contact tables/history/index/FK; migration được reviewer kiểm tra và `Test-ContactRequestMigrationSafety.ps1` tạo report pass. |
| Docker empty DB | SQL healthy; migrator `exited (0)`; `GET /health` HTTP 200; lưu `ps` + migrator logs. |
| Frontend/demo | `/contact`, login, `/admin/contact-requests`; desktop/iPhone/360px evidence nếu hồ sơ yêu cầu. |

## B. Điều kiện để merge PR TV3 vào `dev`

1. PR target đúng: `feature/contact-request-management` → `dev`; không phải `main`.
2. PR Description nêu scope, shared-file hunk, command/evidence test, migration/Docker status và checklist chưa hoàn tất.
3. GitHub Actions của **commit mới nhất** xanh; nếu conflict/rebase/merge dev vừa xảy ra thì evidence/test phải chạy lại.
4. Collaborator (ưu tiên reviewer TV2 theo phân công) submit review thực chất và approval; Copilot không thay thế review này.
5. Không còn unresolved conversation, required check failed, secret `.env`, migration drift, hoặc file ngoài scope.
6. TV3 gửi URL PR thật cho nhóm; nhóm trưởng/reviewer xác nhận PR hợp lệ để tính vào mục tiêu tối thiểu 10 PR. PR trùng/upload nhầm (#1, #3, #6 theo quy ước nhóm) không được tính.

Chỉ người có quyền merge theo quy trình nhóm merge vào `dev`. Không tạo commit rỗng, không đổi author, không force-push để né review.

## C. Chuẩn bị release `dev` → `main`

Sau khi các feature hợp lệ đã merge vào `dev`, nhóm trưởng thực hiện release review trên **HEAD hiện tại của `dev`**:

| Gate release | Điều kiện |
|---|---|
| Feature completeness | Thành viên có code/commit/PR/review thật; các module yêu cầu đã merge vào `dev`. |
| Integration regression | Restore, Release build, full test/coverage, frontend lint/build/E2E, Docker database rỗng và health chạy lại trên `dev` tích hợp. |
| Migration safety | Migrations của toàn nhóm apply được từ database rỗng và không có model drift/bảng ngoài scope. |
| Security/configuration | Không có `.env`/token/password thật; secrets được cấu hình ngoài Git; rate limit/auth/CORS vẫn pass regression. |
| Release evidence | Link CI xanh, PR reviews, Docker logs, screenshots demo và báo cáo/test artifacts được lưu. |
| Merge to main | Nhóm trưởng mở PR `dev` → `main`, review release, merge khi các gate trên pass. |

`main` không tự nhận push từ TV3/TV4. Sau release, tạo tag/version theo convention của nhóm, lưu SHA release và dùng đúng artifact đã được CI build.
