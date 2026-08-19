# Playbook demo và xử lý sự cố — Contact Request TV3

## Mục tiêu demo

Demo trình bày đúng scope TV3: khách công khai gửi yêu cầu Contact, Admin/Editor quản lý danh sách/history và chuyển trạng thái theo workflow. Không demo Landing, News, Customer, Affiliate, Order, Auth internals hoặc dữ liệu review/testimonial giả.

## Kịch bản demo 7 phút

| Thời lượng | Thao tác | Lời trình bày ngắn | Kết quả cần thấy |
|---:|---|---|---|
| 0:00–0:45 | Mở `/contact` | “Đây là form public Contact Request, không cần đăng nhập.” | Form có required field, accessibility label, CTA. |
| 0:45–1:30 | Gửi valid request kỹ thuật | “Backend normalize input, kiểm duplicate 24h và tạo mã request.” | Success state + request ID. |
| 1:30–2:00 | Gửi lại email cùng nội dung | “Duplicate window trả conflict thay vì tạo spam request.” | `409`/error message rõ. |
| 2:00–2:30 | Thử invalid email hoặc message ngắn | “DataAnnotations + Application validation trả ProblemDetails.” | `400`/validation message. |
| 2:30–3:15 | Đăng nhập account Admin/Editor thật trên demo env rồi mở `/admin/contact-requests` | “Route guard chỉ cho Admin/Editor.” | List/filter/paging Contact. |
| 3:15–4:00 | Mở detail | “Detail bao gồm message, metadata và status history.” | History/timeline hiển thị. |
| 4:00–5:00 | `Pending → Contacted → Approved` | “Transition authoritative ở backend; terminal không reopen.” | Status/history thay đổi. |
| 5:00–5:30 | Thử Reject không note trên request kỹ thuật khác | “Rejected/Cancelled yêu cầu note.” | `400`, request không đổi status. |
| 5:30–6:15 | Thử role Customer/anonymous qua smoke/Postman | “Public chỉ create; quản trị bị policy chặn.” | `401`/`403` đúng. |
| 6:15–7:00 | Nêu migration/Docker/CI gate | “Migration chỉ được sinh từ dev thật; không tuyên bố pass nếu thiếu log.” | Checklist/evidence thực tế. |

## Pre-demo checklist

- [ ] Đang dùng database disposable/demo đã được phép, không dùng staging/prod thật cho thử nghiệm.
- [ ] `docker compose ... ps -a` cho SQL healthy, migrator exit 0, API running.
- [ ] `GET /health` trả 200.
- [ ] Có một Admin/Editor demo account hợp lệ và không hiển thị password trên slide/screen.
- [ ] Có một request kỹ thuật mới và một request riêng cho reject/cancel validation.
- [ ] Không hiển thị token, `.env`, connection string, JWT, raw log hoặc thông tin khách hàng.
- [ ] Browser 360px có ảnh/responsive evidence nếu giảng viên yêu cầu; ghi nhãn local/mock nếu ảnh không dùng backend thật.

## Bảng xử lý sự cố nhanh

| Triệu chứng | Kiểm tra đầu tiên | Cách xử lý an toàn |
|---|---|---|
| API không lên / health khác 200 | `docker compose ... ps -a`, log `migrator`, log `api` | Không reset volume nhóm; dùng volume Contact-empty và đọc lỗi migration. |
| Migrator không exit 0 | SQL healthcheck, connection env, migration generated diff | Dừng; review migration Contact-only, không sửa migration tay cho chạy qua. |
| `/admin/contact-requests` redirect | Hunk `admin-nav.ts`, roles Admin/Editor, refresh session | Merge đúng item Contact Admin/Editor; không nới route guard toàn cục. |
| Customer thấy admin 403 | Đây là expected | Dùng Admin/Editor demo account đúng quyền. |
| Create trả 409 | Email đã gửi trong 24h | Dùng email kỹ thuật mới `@example.test`, không xóa data production. |
| Create trả 429 | Đã vượt Contact IP limit | Chờ window/thay environment demo được phép; không tắt rate limit để demo. |
| E2E Chromium crash | `free -h`, worker count, trace | Chạy `Run-ContactE2ELowResource.sh`; không tự coi crash là source pass/fail. |
| CORS staging fail | `FRONTEND_STAGING_ORIGIN`, `NEXT_PUBLIC_SITE_URL`, Compose preflight | Exact origin phải trùng scheme/host/port; không wildcard. |

## Evidence cần lưu sau demo thật

Lưu timestamp, SHA branch, command chạy, Docker `ps`, migrator exit/log đã che secret, health response, smoke output, E2E output và screenshot. Raw file không mặc định commit vào PR; đính bằng CI artifact hoặc hồ sơ nộp theo quy định nhóm.
