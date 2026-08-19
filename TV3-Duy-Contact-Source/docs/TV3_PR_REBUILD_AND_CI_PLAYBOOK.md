# Playbook sửa build và CI — PR Contact thay thế

## 1. Lấy lỗi gốc trước khi sửa code

Không đoán lỗi dựa trên việc `dotnet build` fail. Trong GitHub Actions run #11, mở step backend build, copy từ dòng lỗi đầu tiên có mã `CSxxxx`, `NUxxxx` hoặc thông báo project/package thiếu cho đến hết stack trace liên quan. Lỗi đầu tiên thường là nguyên nhân gốc; các lỗi sau có thể là cascade.

Trên clone `dev` thật, chạy lại để tái hiện có log:

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore -v:minimal
```

Lưu toàn bộ stdout/stderr hoặc upload file log. Không thay đổi code trước khi biết lỗi biên dịch đầu tiên.

## 2. Thứ tự sửa và kiểm tra

| Thứ tự | Lệnh | Điều kiện qua bước |
|---:|---|---|
| 1 | `dotnet restore CloudServiceStore.sln` | Restore không lỗi package/feed. |
| 2 | `dotnet build CloudServiceStore.sln --configuration Release --no-restore` | Build exit code 0. |
| 3 | `dotnet test CloudServiceStore.sln --configuration Release --no-build` | Test có report/log thật. |
| 4 | `cd frontend && npm ci && npm run lint && npm run build` | Cả ba command exit code 0. |
| 5 | Docker DB rỗng | Migrator exit 0, API `/health` 200, migration chỉ Contact. |
| 6 | Postman/browser | Public create, duplicate, Admin/Editor status, Customer 403, terminal 409. |

Không bỏ qua build bằng cách tắt project, nới nullable, xóa test hoặc đổi CI thành `continue-on-error`.

## 3. Điểm Contact phải kiểm tra sau khi build qua

Controller/API client phải cùng `POST /api/v1/contact-requests/{id}/status`; enum phải giữ năm trạng thái `Pending`, `Contacted`, `Approved`, `Rejected`, `Cancelled`; policy `ManageContactRequests` chỉ cho Admin/Editor; terminal status không reopen; Rejected/Cancelled bắt buộc note; duplicate window 24 giờ; audit create/status-change; rate limit phân vùng theo IP; response lỗi dùng `ProblemDetails`.

## 4. Quy tắc shared-file diff

PR Contact mới chỉ được có các hunk Contact đã ghi tại `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Không được đưa lại `AuthSecurityExtensions.cs`, `site-header.tsx`, `admin-nav.ts` nếu không có yêu cầu UI Contact tối thiểu được reviewer chấp thuận.

```bash
git diff --check
git diff origin/dev...HEAD -- \
  src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs \
  src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/appsettings.json \
  frontend/src/lib/api.ts docker-compose.yml
```

## 5. PR và review thật

Mở PR thay thế từ `feature/contact-request-management` vào `dev` sau khi local build/test pass. PR body phải link log CI, checklist đã tick sau khi có evidence, mô tả migration và liệt kê shared hunk. Request ít nhất một reviewer; reviewer phải submit review hoặc comment rõ ràng về architecture, authorization, validation, test, regression và shared-file safety. PR cũ closed/unmerged không được xem là evidence review hay merge.
