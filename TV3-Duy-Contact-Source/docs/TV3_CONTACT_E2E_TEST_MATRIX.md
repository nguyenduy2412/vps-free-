# Ma trận E2E Playwright — Contact Request TV3

## Môi trường chạy

Playwright chạy cùng Next.js app ở cổng riêng `3100`, có ba project: Desktop Chromium, iPhone 13 Chromium và Chromium viewport 360 × 800. Mỗi case chạy trên cả ba project, tổng **27/27 pass**.

| ID | Case E2E | Biên được kiểm tra | Kết quả |
|---|---|---|---|
| E2E-01 | Public submit success | `POST`, payload camelCase đầy đủ, response `201`, request ID hiện ra, reset form qua “Gửi yêu cầu khác”. | Pass × 3 |
| E2E-02 | Duplicate conflict | `409 ProblemDetails` hiển thị message và nút submit được enable lại. | Pass × 3 |
| E2E-03 | Server validation | `400 ProblemDetails.errors` được trích và hiển thị cho người dùng. | Pass × 3 |
| E2E-04 | Rate limit | `429 ProblemDetails` hiển thị, submit được enable lại và có thể retry. | Pass × 3 |
| E2E-05 | Native required | Form rỗng không gọi API; browser phát hiện input/textarea required không hợp lệ. | Pass × 3 |
| E2E-06 | Loading/double-submit | Khi request pending, nút đổi “Đang gửi yêu cầu...”, disabled và chỉ tạo một request. | Pass × 3 |
| E2E-07 | Responsive 360 px | Trang Contact không có horizontal overflow; heading hiển thị ở mobile. | Pass × 3 |
| E2E-08 | Native email validation | Email sai định dạng bị browser chặn, không gọi API. | Pass × 3 |
| E2E-09 | Network failure | Browser abort request hiển thị fallback message tiếng Việt và enable submit để retry. | Pass × 3 |

## Giới hạn có chủ đích

E2E public form mock network tại browser layer. Cách này kiểm tra UI/API client/error handling mà không cần Docker/SQL Server. Luồng backend thật, authorization Admin/Editor, migration SQL Server và Docker database rỗng được cover bằng backend tests/runner riêng và vẫn phải chạy trên clone `dev` thật trước merge. Các giới hạn tối thiểu/tối đa nghiệp vụ, duplicate 24 giờ, workflow và audit không lặp lại ở browser E2E vì đã có unit/controller/integration test backend; E2E chỉ xác minh UI phản ứng đúng với response backend.
