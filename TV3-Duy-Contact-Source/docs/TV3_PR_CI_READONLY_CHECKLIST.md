# Checklist PR và CI — TV3 Contact Request (read-only)

> Checklist này hướng dẫn kiểm tra trong clone local và giao diện GitHub ở chế độ đọc. Nó không chứa lệnh `git push`, `gh pr create`, merge, approve, xóa file hoặc chỉnh sửa repository `HieuIPO/CloudServiceStore-Team-Contributions`.

## 1. Trước khi TV3 tự commit/push bằng account thật

- [ ] Xác nhận branch hiện tại là `feature/contact-request-management`, được tạo từ latest `dev`.
- [ ] Chạy `git status --short`; chỉ còn source/test/script/docs Contact và shared hunk Contact cần thiết.
- [ ] Chạy `git diff --check origin/dev` không có whitespace error.
- [ ] Xem `git diff --name-only origin/dev` và `git diff origin/dev` toàn bộ.
- [ ] Xem riêng `Program.cs`, `DependencyInjection.cs`, `CloudServiceStoreDbContext.cs`, `frontend/src/lib/api.ts`, `docker-compose.yml`, `admin-nav.ts`; chỉ giữ hunk Contact.
- [ ] Không có `AuthSecurityExtensions.cs`, News, Landing, Customer, Order, Affiliate, Header/Nav ngoài Contact.
- [ ] Migration EF được sinh từ branch này, review `Up()`/`Down()` Contact-only và `Test-ContactRequestMigrationSafety.ps1` pass.
- [ ] Docker Contact-empty có SQL healthy, migrator exit 0, API health 200 và smoke expected status.
- [ ] Re-run build/test/coverage/npm ci/lint/frontend build/E2E trên SHA branch hiện tại.
- [ ] Raw log, coverage XML, trace, token, `.env`, screenshot temporary không nằm trong staging area.

## 2. Sau khi TV3 tự mở PR vào `dev`

Trên GitHub, chỉ **xem** các mục sau. Không tự gửi comment/review/approve nếu bạn không phải collaborator được phân công.

| Mục cần xem | Điều kiện đạt |
|---|---|
| Base/head | Base là `dev`; head là `feature/contact-request-management`. |
| Commit cuối | SHA hiển thị trên PR khớp SHA local đã test. |
| Files changed | Chỉ source/test/migration/docs Contact và shared hunk cần thiết; không full shared replacement. |
| Checks | Run mới của PR SHA cuối pass: restore, Release build, backend test/coverage, npm ci, lint, frontend production build, Docker build. |
| Migration evidence | Link/log artifact cho migration Contact-only, DB rỗng/migrator/health nếu pipeline/máy nhóm chạy. |
| Review | Collaborator/TV2 review thực chất về architecture, authorization, validation, workflow, audit, migration, tests, regression, shared diff. |

## 3. Khi CI/review fail

- [ ] Đọc job/step/log ở chế độ xem; ghi lại SHA, job name, lỗi đầu tiên và phạm vi.
- [ ] Nếu lỗi ở Contact, sửa trên **local feature branch**, chạy lại relevant test/diff rồi để TV3 tự commit/push.
- [ ] Nếu lỗi baseline/module khác, ghi rõ owner và không trộn fix ngoài scope vào PR Contact.
- [ ] Sau khi TV3 push commit mới, mọi check/review cần gắn với SHA mới; run pass cũ không đủ.
- [ ] Không tự approve, không merge và không khai “CI pass” khi run SHA cuối vẫn pending/fail/không tồn tại.

## 4. Điều kiện merge

Chỉ nhóm trưởng/collaborator được quyền merge sau khi migration Contact-only, Docker DB rỗng, build/test/frontend/Docker checks, diff scope, CI SHA cuối và review không blocking đều có evidence thật. Source package hoặc historical local result không thay các điều kiện này.
