# Checklist PR thay thế — Contact Request

## Phạm vi diff cho phép

PR mới chỉ bao gồm source Contact, test Contact, route/UI Contact, Docker override/scripts và docs Contact. Với file shared, chỉ áp dụng hunk đã ghi trong `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Không đưa `AuthSecurityExtensions.cs`, `site-header.tsx`, `admin-nav.ts` hay thay đổi News/Landing/Auth không cần thiết vào PR.

## Checklist evidence trước khi mở PR

| Hạng mục | Evidence phải đính kèm | Trạng thái hiện tại |
|---|---|---|
| Restore | Log `dotnet restore` exit 0 | Chưa có từ baseline thật. |
| Backend build | `build-release.log`, exit 0 | Blocked: run #11 fail, log chi tiết 404. |
| Backend tests/coverage | Log test + artifact coverage | Chưa chạy sau build failure. |
| Frontend install/lint/build | Log `npm ci`, lint, build exit 0 | Chưa chạy sau build failure. |
| Migration | Migration mới từ `dev`, review chỉ Contact | Chưa được sinh lại. |
| Docker DB rỗng | Migrator exit 0, `/health` 200 | Chưa chạy. |
| Public/Admin UI | Ảnh/video thật: create, list, history, status | Chưa xác minh end-to-end. |
| Responsive 360 px | Ảnh browser thật | Chưa xác minh. |
| Security | Test/log authorization, duplicate, terminal, rate-limit | Chưa xác minh sau build. |
| Reviewer | Review/comment thật của collaborator | PR #8 có 0 review. |
| CI | Link run xanh của PR mới | PR #8 fail/closed/unmerged. |

## PR body tối thiểu sau khi evidence có thật

```markdown
## Contact Request Management (TV3)

### Phạm vi
- Public `POST /api/v1/contact-requests` với IP rate-limit.
- Admin/Editor list, detail/history và `POST /api/v1/contact-requests/{id}/status`.
- Workflow Pending → Contacted → Approved hoặc Rejected/Cancelled; terminal không reopen.

### Shared-file safety
- Chỉ merge DbSet Contact, hai DI registration, named Contact rate limiter, policy Contact, config Contact và API client hunk.
- Không thay thế AuthSecurity, Navigation/Header, News, Landing hoặc Docker foundation.

### Evidence
- [x] Restore: <link/log>
- [x] Release build: <link/log>
- [x] Tests + coverage: <link/artifact>
- [x] Frontend lint/build: <link/log>
- [x] Docker empty database: <link/log>
- [x] Migration reviewed: <link/diff>
- [x] Responsive 360 px: <link/image>
- [x] Reviewer submitted review: <link>
```

Chỉ tick một checkbox sau khi link/log/artifact tương ứng tồn tại. PR #8 không được dùng làm evidence pass vì run fail và PR đã đóng không merge.
