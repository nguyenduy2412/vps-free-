# Đối chiếu cuối — Contact Request TV3 với quy ước và bảng phân công

## Nguồn đối chiếu

Tài liệu này đối chiếu module `feature/contact-request-management` với `Quy_uoc_ky_thuat_dung_chung_CloudServiceStore.docx` và `Bang_phan_cong_cong_viec_CloudServiceStore.docx` do nhóm cung cấp.

## Phạm vi TV3

Bảng phân công giao TV3/Duy ba branch: Landing/Customers, News và Contact Request. Archive cuối này cố ý chỉ đóng gói **Contact Request** vì mục tiêu là branch `feature/contact-request-management`; không đưa code Landing/News vào để tránh lẫn phạm vi hoặc ghi đè phần đã triển khai ở branch riêng.

## Đối chiếu yêu cầu kỹ thuật

| Yêu cầu | Trạng thái Contact TV3 |
|---|---|
| Clean Architecture Domain/Application/Infrastructure/WebApi | Đạt: entity/enum, contracts/service, configuration/repository, controller tách lớp. |
| API `/api/v1`, camelCase, DTO/PagedResult/ProblemDetails | Đạt trong source Contact. |
| Admin/Editor quản lý Contact | Đạt: policy `ManageContactRequests`, controller và UI admin. |
| Workflow, audit, rate limit | Đạt trong source: status history, audit, duplicate 24h, rate limiter IP. |
| Form public và responsive | Source có public form/admin UI; Playwright 360px không overflow pass, chưa có ảnh screenshot nộp kèm. |
| Unit/integration/E2E tests | Đạt khi chạy sandbox: Domain 22, Application 121, Integration 80, tổng 223 pass; Playwright Contact 27/27 pass. |
| Backend build + frontend lint/build | Đạt khi chạy sandbox sau sửa lỗi: Release build 0 warning/0 error; npm ci/lint/build pass với `NODE_ENV` chuẩn. |
| Migration SQL Server từ dev | Chưa hoàn tất: phải sinh bằng `dotnet ef` từ clone `dev` thật và review Contact-only. |
| Health endpoint và Docker migrator/database rỗng | `AddHealthChecks`/`MapHealthChecks("/health")`, SQL healthcheck và migrator riêng đã có source; Docker database rỗng chưa chạy vì sandbox không có Docker CLI/daemon. |
| PR/review chéo | Chưa hoàn tất: PR #8 closed/unmerged; TV3 phải tạo branch mới từ `dev`, commit/push bằng GitHub identity thật, mở PR vào `dev`, nhận review collaborator/TV2 thực chất và CI xanh. Không push trực tiếp `main` hoặc tạo PR/commit giả. |
| Hồ sơ | Có README, report Markdown/PDF, demo script, presentation script, Postman, evidence/PR/migration docs. Không có slide deck/ảnh Docker-360px chạy thật trong archive. |

## Quy tắc không vi phạm

1. Không copy/ghi đè toàn bộ file shared; chỉ merge hunk Contact theo exact merge guide.
2. Không lấy `AuthSecurityExtensions.cs`, Header/Nav, News, Landing, Order, Affiliate hoặc Auth vào PR Contact.
3. Không commit `.env`, password, token, connection string thật, `node_modules`, `.next`, `bin` hoặc `obj`.
4. Không dùng migration Contact thủ công/archive cũ; migration chính thức sinh từ `dev` sau build/test.
5. Không nói migration/Docker/CI/review đã pass khi chưa có evidence thực từ clone/PR GitHub thật.
6. Không push trực tiếp `main`; `main` chỉ là release do nhóm trưởng quyết định merge sau khi feature trên `dev` đạt điều kiện.

## Checklist merge theo bảng phân công

Trước merge PR mới: reviewer TV2 submit review thực chất; migration Contact-only được review; Docker DB rỗng kiểm tra migrator exit 0 và health 200; CI GitHub xanh; PR có evidence build/test/frontend/migration/Docker/responsive. Chỉ sau đó module mới đạt DoD theo tài liệu nhóm.
