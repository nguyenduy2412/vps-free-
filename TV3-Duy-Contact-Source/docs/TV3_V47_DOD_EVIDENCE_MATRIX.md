# Ma trận Definition of Done — TV3 Contact Request v47

> **Kết luận:** v47 khắc phục hai gap có thể tái hiện trên snapshot: có assertion audit không leak full `ContactRequest.Message`, và route `/admin/contact-requests` được allow đúng cho Admin/Editor bằng hunk navigation tối thiểu. Các gate migration/Docker/dev/GitHub vẫn **chưa có evidence** và không được gắn nhãn pass.

## 1. Evidence đã chạy trong workspace hiện tại

| Gate | Kết quả | Evidence thực tế | Giới hạn bắt buộc nêu rõ |
|---|---|---|---|
| Release build | Pass | `dotnet build ... Release --no-restore`: 0 warning, 0 error. | Snapshot, không phải branch `dev`/PR. |
| Backend test | Pass | 225/225: Domain 22, Application 123, Integration 80. | Snapshot, không phải CI commit PR. |
| Audit privacy | Pass | Hai test mới xác nhận `NewValuesJson`/`OldValuesJson` không chứa full message Contact. | Kiểm tra unit/mock repository; không phải query audit DB thật. |
| Authorization/validation/rate limit test | Có test integration pass trong 80 integration test. | Anonymous list `401`, Customer `403`, Editor `200`, controller ProblemDetails `400/401/409/429`, HTTP rate-limit `429`. | Snapshot; API Docker/staging thật vẫn chưa chạy. |
| Controller architecture guard | Pass tĩnh | `Check-ContactControllerArchitecture.sh` xác nhận service-only và không có EF/SQL persistence symbol; thay thế audit false positive cũ. | Không thay thế review source trên `dev` thật. |
| Frontend lint | Pass | `npm run lint`. | Snapshot. |
| Frontend production build | Pass | `NODE_ENV=production npm run build`, 25 route. | Snapshot; dùng `NODE_ENV=production` để tránh environment shell không chuẩn. |
| Public Contact E2E | Pass | 27/27, chạy tuần tự `--workers=1` trên desktop, iPhone và 360px. | Mock browser-layer API, không thay API Docker. |
| Evidence UI 360px | Pass | 4/4: public/admin × desktop/360px; page không horizontal overflow. | Session/data `@example.test` mock; không phải Docker/staging/authorization API thật. |

Lần chạy E2E mặc định ba worker từng có một Chromium crash do tài nguyên sandbox, không phải assertion Contact fail. Lần chạy lại tuần tự `--workers=1` đạt 27/27; CI có đủ tài nguyên có thể dùng worker mặc định, còn runner hạn chế nên đặt `--workers=1` để evidence ổn định.

## 2. Hunk integration mới bắt buộc review

| Shared file | Hunk tối thiểu | Lý do |
|---|---|---|
| `frontend/src/components/admin/admin-nav.ts` | Import `IconMessageCircle` và item `/admin/contact-requests` cho `Admin`, `Editor`. | `AdminSessionProvider` sử dụng `isRouteAllowed` dựa trên `adminNavGroups`; thiếu item làm route Contact bị redirect dù page tồn tại. |

Không đưa nguyên file `admin-nav.ts` vào archive TV3-only. Merge đúng hunk trong `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`; audit diff không được có thay đổi Dashboard, Order, Affiliate hay Nav item khác.

Contract endpoint status giữ cố định là **`POST /api/v1/contact-requests/{id}/status`** trong controller, API client và tài liệu. Không đổi lại thành PATCH theo tài liệu/status module cũ.

## 3. Gate còn thiếu hoàn toàn và không được tuyên bố pass

| Gate bắt buộc | Trạng thái | Điều kiện để đóng gate |
|---|---|---|
| Checkout branch `dev` thật + branch TV3 | Blocked | Git worktree có `origin`, `git fetch`, `git switch dev`, `git pull --ff-only`, branch `feature/contact-request-management`. |
| Shared diff Contact-only | Blocked | `git diff --check` và diff từng file shared trên branch thật; chỉ hunk TV3. |
| Migration Contact-only | Blocked | Sinh lại bằng `dotnet ef migrations add` trên `dev` đã merge; review `Up/Down` chỉ Contact tables/index/FK. |
| SQL Server DB rỗng/migrator/health | Blocked | Docker daemon: SQL healthy, migrator exit 0, API running, `/health` 200. |
| Migration safety DB disposable | Blocked | Chạy test/script với `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` guarded, kiểm tra schema/FK/index/rollback. |
| API security evidence thật | Blocked | Token hợp lệ: anonymous public POST allowed, Customer `403` list/status, Editor/Admin allowed, duplicate `409`, rate-limit `429`. |
| Commit/push/PR/CI/review thật | Không thực hiện theo giới hạn GitHub hiện tại | Chỉ thực hiện khi user cho phép write remote; reviewer collaborator và CI phải là evidence trên PR commit cuối. |

## 4. Hướng dẫn evidence nộp được

Chọn bốn PNG trong `artifacts/v46-gap-closure/responsive-screenshots/`; tối thiểu cần `/contact` 360px, `/admin/contact-requests` 360px và một desktop. Khi đưa vào hồ sơ, ghi chú đúng: **UI local mock evidence**, không phải evidence backend/staging. Sau khi Docker/staging thật chạy, thay/bổ sung evidence không mock theo `TV3_CONTACT_LOCAL_SMOKE_E2E_360_RUNBOOK.md` và checklist staging.
