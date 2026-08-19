# PR title

```text
feat(contact): add contact request management
```

# PR body — copy từ dòng dưới đây

```markdown
## Mục tiêu

PR này triển khai module **Contact Request Management** cho phần việc TV3/Duy. Module cho phép khách công khai gửi yêu cầu liên hệ/tư vấn, đồng thời cho phép **Admin/Editor** xem danh sách, xem chi tiết/lịch sử và cập nhật trạng thái theo workflow có kiểm soát.

## Phạm vi chức năng

### Public Contact

- `POST /api/v1/contact-requests` cho phép anonymous submit.
- Form `/contact` có label, validation, loading, error/success state và `aria-live`.
- Chống gửi trùng theo email trong cửa sổ 24 giờ.
- Rate limit theo IP bằng named policy `contact-requests`.

### Admin/Editor Contact Management

- `GET /api/v1/contact-requests` hỗ trợ search, status filter và pagination.
- `GET /api/v1/contact-requests/{id}` trả chi tiết và status history.
- `POST /api/v1/contact-requests/{id}/status` cập nhật trạng thái.
- Trang `/admin/contact-requests` có list, filter, detail, history và action workflow.
- Policy `ManageContactRequests` chỉ cho role `Admin` và `Editor`.

### Workflow và validation

- Status: `Pending -> Contacted -> Approved`, hoặc `Rejected` / `Cancelled` theo transition hợp lệ.
- Không cho reopen terminal status.
- `Rejected` và `Cancelled` bắt buộc có note.
- Validation service đồng bộ với DTO: full name 2–160, subject 3–180, message 10–4000, email/phone hợp lệ, page size 1–100.
- Ghi audit cho create và status change, không ghi full message vào audit.

## Kiến trúc và file chính

| Tầng | Nội dung |
|---|---|
| Domain | `ContactRequest`, `ContactRequestStatusHistory`, `ContactRequestStatus` |
| Application | DTO/contracts, repository abstraction, service validation/workflow/audit |
| Infrastructure | EF configuration, repository, index theo truy vấn Contact |
| Web API | Controller, authorization policy, named Contact rate limiter |
| Frontend | Public form, Admin/Editor management page, API client hunk |
| Tests | Service, controller, HTTP rate-limit, pagination/status filter, authorization và SQL Server migration test |

## Shared-file safety

PR chỉ giữ các hunk Contact cần thiết trong file shared:

- `CloudServiceStoreDbContext.cs`: hai `DbSet` Contact.
- `DependencyInjection.cs`: hai scoped registrations Contact.
- `Program.cs`: `AddContactRequestRateLimiting` và policy `ManageContactRequests`.
- `appsettings.json`: `ContactPermitLimit` (seed flag chỉ khi nhóm phê duyệt demo local).
- `frontend/src/lib/api.ts`: Contact types + `contactRequestsApi` dùng **POST** status endpoint.
- `docker-compose.yml`: seed flag tùy chọn và hunk healthcheck `SELECT 1` để migrator chạy được trên database mới; không sửa image/volume/port/command migrator.

Không thay thế toàn file shared. Không đưa `AuthSecurityExtensions.cs`, `site-header.tsx`, `admin-nav.ts`, News, Landing, Order, Affiliate hoặc Auth changes ngoài phạm vi Contact vào PR này.

## Sửa lỗi phát hiện khi chạy thực tế

- Sửa `CS1061` ở `ContactRequestSqlServerMigrationTests`: `GetSchema("Tables")` là API đồng bộ, không await.
- Chuyển DataAnnotations record DTO từ `[property: ...]` sang `[param: ...]` để ASP.NET Core .NET 10 áp dụng validation primary-constructor đúng cách.
- Đồng bộ validation nghiệp vụ service với min length của DTO để unit test và API contract nhất quán.

## Kiểm tra đã chạy thật

| Lệnh / kiểm tra | Kết quả |
|---|---|
| `dotnet restore CloudServiceStore.sln` | Pass |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` | Pass — 0 warning, 0 error |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` | Pass — Domain 22/22, Application 121/121, Integration 80/80; tổng 223/223 |
| `cd frontend && npm ci` | Pass (npm audit báo 6 high-severity dependency vulnerabilities, chưa chạy audit fix) |
| `cd frontend && npm run lint` | Pass |
| `cd frontend && env -u NODE_ENV npm run build` | Pass; `/contact` và `/admin/contact-requests` build thành công |

## Migration và Docker

Migration Contact **không dùng migration thủ công từ archive**. Trước khi merge, cần sinh migration mới từ branch `dev` đã cập nhật, review để bảo đảm chỉ thay đổi `ContactRequests`, `ContactRequestStatusHistories`, index và FK Contact:

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj \
  --startup-project src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj \
  --output-dir Persistence/Migrations
```

Docker/database rỗng chưa chạy trong sandbox vì không có Docker CLI/daemon. Cần chạy lại trên máy nhóm hoặc CI với `docker-compose.contact-empty.yml` trước merge.

## Checklist trước merge

> Các số build/test/lint/E2E trong source bundle là **historical snapshot evidence**. Không tick các mục runtime/Git bên dưới chỉ vì ZIP có log cũ; chỉ tick sau khi chạy trên commit cuối của PR mới.

- [ ] `dotnet restore`, Release build và toàn bộ test/coverage pass trên branch `feature/contact-request-management` từ `dev` thật.
- [ ] `npm ci`, lint, production build và Contact E2E pass trên branch PR; ghi SHA/command mới.
- [ ] `git diff --name-only origin/dev...HEAD` và `git diff origin/dev...HEAD` đã review; shared file chỉ có hunk Contact cần thiết trong `Program.cs`, `DependencyInjection.cs`, `CloudServiceStoreDbContext.cs`, `frontend/src/lib/api.ts`, `docker-compose.yml` và `admin-nav.ts` khi cần.
- [x] Không có migration Contact thủ công trong source contribution.
- [ ] Sinh migration Contact từ `dev` thật và review diff migration.
- [ ] Chạy Docker/database rỗng, kiểm tra migrator exit 0 và `/health` 200.
- [ ] Kiểm tra responsive trên branch/demo environment ở viewport 360px và đính kèm ảnh có nhãn phạm vi evidence.
- [ ] CI GitHub của PR mới xanh.
- [ ] Có review thật của collaborator.

PR phải được tạo từ `feature/contact-request-management` vào `dev`; không push trực tiếp vào `main`. Commit/push/PR phải dùng account/email GitHub thật của TV3, và chỉ merge sau khi CI xanh cùng collaborator review thực chất.

## Hướng dẫn reviewer

Xin reviewer tập trung vào các điểm sau:

1. Architecture: Domain → Application → Infrastructure → Web API → frontend có bám convention hiện có không.
2. Authorization: `ManageContactRequests` có chỉ cho Admin/Editor; Customer/anonymous không truy cập admin APIs.
3. Validation: DataAnnotations primary-constructor và service validation có đồng bộ; duplicate 24h, note terminal status và transition có đúng không.
4. Security: rate limit IP named policy có không thay body 429 của Login/Order/Affiliate; audit không lộ full message.
5. Regression: shared-file diff có đúng hunk Contact, không thay thế Auth/Header/Nav/News/Landing không liên quan.
6. Migration: migration sinh từ `dev` chỉ tạo bảng/index/FK Contact.

## Demo nhanh

1. Gửi form tại `/contact`, nhận request id.
2. Đăng nhập Admin/Editor, mở `/admin/contact-requests`.
3. Search/filter request, mở detail và history.
4. Chuyển `Pending -> Contacted -> Approved`; thử transition terminal không hợp lệ.
5. Thử `Rejected`/`Cancelled` thiếu note để nhận validation error.
6. Thử Customer gọi admin API để xác minh `403`.
7. Submit quá permit limit từ cùng IP để xác minh `429`.
```
