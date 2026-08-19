# Báo cáo tiến độ và kiểm chứng — Contact Request Management

## 1. Phạm vi module

Module **Contact Request Management** thuộc branch `feature/contact-request-management` của TV3/Duy. Phạm vi chỉ gồm form liên hệ công khai, quản trị Contact cho Admin/Editor, workflow trạng thái, audit, rate limit, persistence, test, tài liệu và hướng dẫn triển khai. Báo cáo không tính News, Landing, Auth, Orders, Affiliate hay các module của thành viên khác là phần đóng góp Contact.

## 2. Kiến trúc và chức năng đã triển khai

| Tầng | Nội dung Contact |
|---|---|
| Domain | `ContactRequest`, `ContactRequestStatusHistory`, enum `Pending`, `Contacted`, `Approved`, `Rejected`, `Cancelled`. |
| Application | DTO/contract, repository abstraction, service validation, duplicate window 24 giờ, workflow và audit. |
| Infrastructure | EF configuration/index, repository, DbSet/DI hunk để merge vào source chung. |
| Web API | Public create, Admin/Editor list/detail/status, `ProblemDetails`, policy `ManageContactRequests`, named IP rate limiter. |
| Frontend | `/contact` public form; `/admin/contact-requests` list/filter/detail/history/workflow action. |
| Test/hồ sơ | Unit, controller, rate-limit, migration test, Playwright public-form E2E, Postman, Docker override, demo script, presentation script, PR/migration runbook. |

## 3. Business rules chính

1. Public request được tạo tại `POST /api/v1/contact-requests`; địa chỉ email không được gửi trùng trong 24 giờ.
2. Chỉ Admin/Editor được list, xem chi tiết/history và gọi `POST /api/v1/contact-requests/{id}/status`.
3. Workflow hợp lệ: `Pending → Contacted → Approved`, hoặc `Rejected`/`Cancelled`; trạng thái cuối không được mở lại.
4. `Rejected` và `Cancelled` bắt buộc có note; mọi create/status-change có audit metadata, không ghi full message vào audit.
5. Validation service và DTO thống nhất: full name 2–160, subject 3–180, message 10–4000; lỗi nghiệp vụ trả `ProblemDetails` phù hợp.

## 4. Lỗi thực tế đã tìm và sửa

| Lỗi | Cách xử lý |
|---|---|
| Release build lỗi `CS1061` ở migration integration test | Bỏ `await` trước ADO.NET `GetSchema("Tables")`, vì API này đồng bộ. |
| API public Contact lỗi record validation metadata trên .NET 10 | Chuyển DataAnnotations của record primary constructor từ `[property: ...]` sang `[param: ...]`. |
| Unit test không chặn message quá ngắn | Đồng bộ service validation với giới hạn tối thiểu trong DTO. |

## 5. Kết quả chạy thật trong sandbox

| Kiểm tra | Kết quả |
|---|---|
| .NET SDK | Cài và chạy bằng .NET SDK `10.0.110`. |
| `dotnet restore CloudServiceStore.sln` | Pass. |
| Release build | Pass — 0 warning, 0 error. |
| Domain tests | 22/22 pass. |
| Application tests | 117/117 pass. |
| Integration tests | 80/80 pass. |
| Tổng tests | **223/223 pass**; coverage artifacts được tạo lại trong `TestResults/` sau final pre-commit audit. |
| `npm ci` | Pass; npm thông báo 6 high-severity dependency vulnerabilities, chưa chạy `npm audit fix`. |
| `npm run lint` | Pass. |
| Production build | Pass khi chạy với `NODE_ENV` unset/production chuẩn; `/contact` và `/admin/contact-requests` build thành công. |
| Playwright E2E | Pass: 27/27 test; public success/reset, `ProblemDetails` 400/409/429, native required/email validation, network failure, loading/double-submit và kiểm tra không overflow tại viewport 360px trên Chromium. |

Các log gốc của các lệnh trên được kèm trong archive ở thư mục `evidence/`. Không dùng claim archive cũ thay cho các log này.

## 6. Shared-file safety và migration

Không ghi đè toàn file shared. Chỉ merge hunk Contact vào DbContext, DependencyInjection, Program, appsettings, API client và Docker Compose theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Không đưa `AuthSecurityExtensions.cs`, Header/Nav, News, Landing, Order hoặc Affiliate vào PR Contact.

Migration Contact không có trong archive dưới dạng file thủ công. Sau khi source được đặt trên clone `dev` thật, phải sinh `AddContactRequestManagement` bằng `dotnet ef`, review để migration chỉ có hai bảng Contact, index Contact và khóa ngoại Contact. Hướng dẫn chi tiết nằm tại `TV3_CONTACT_EF_MIGRATION_RUNBOOK.md`.

## 7. Hạng mục chưa hoàn tất trước merge

| Hạng mục | Trạng thái cần xử lý |
|---|---|
| Migration SQL Server từ `dev` thật | Chưa sinh/review. |
| Docker database rỗng | Chưa chạy vì sandbox không có Docker CLI/daemon. |
| Responsive 360 px | Playwright viewport 360px pass, chưa có ảnh screenshot nộp kèm. |
| CI GitHub PR mới | Chưa chạy trên repository chính thức. |
| Review chéo | Chưa có; bảng phân công chỉ định TV2 review TV3. |
| Slide deck và ảnh demo thật | Có presentation/demo script, chưa có slide deck/ảnh chạy thật trong archive. |

## 8. Kết luận

Source Contact đã có evidence local cho build, test, lint và production build sau khi sửa lỗi thực tế. Module sẵn sàng để tạo branch/PR mới, nhưng **chưa đạt Definition of Done để merge** cho đến khi migration Contact-only, Docker database rỗng, responsive evidence, CI GitHub xanh và review chéo TV2 được hoàn tất.
