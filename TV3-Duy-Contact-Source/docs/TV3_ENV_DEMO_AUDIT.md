# Audit `.env` trước demo Docker — Contact Request TV3

**Phạm vi:** `.env.example`, `docker-compose.yml`, `docker-compose.contact-empty.yml` và `scripts/Run-ContactRequestEmptyDatabase.ps1`.

## Kết luận

Tệp `.env` thật phải nằm ở **thư mục gốc** cạnh `docker-compose.yml`, không đặt trong `frontend/`, không đưa vào ZIP TV3-only và không commit Git. Template `.env.example` chỉ có placeholder; không có connection string production hay secret thật trong script PowerShell.

| Biến | Cần cho demo Contact | Kiểm tra trước chạy |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Bắt buộc | Thay placeholder bằng password SQL Server mạnh, có chữ hoa, chữ thường, số và ký tự đặc biệt. API/migrator nhận password này qua Compose. |
| `SEED_ADMIN_PASSWORD` | Bắt buộc nếu cần đăng nhập Admin/Editor | Thay placeholder trước lần tạo database rỗng đầu tiên. Database initializer dùng nó để seed `admin@cloud.local` và `editor@cloud.local`. |
| `JWT_SIGNING_KEY` | Bắt buộc | Thay placeholder bằng chuỗi local riêng dài tối thiểu 32 ký tự. |
| `NEWSDATA_API_KEY` | Không cần cho luồng Contact | Để trống khi không demo News. Compose vẫn nhận giá trị rỗng hợp lệ. |
| `SEED_TV3_LOCAL_DATA` | Tùy chọn | Giữ `false` để demo form public tự tạo dữ liệu; chỉ dùng `true` khi cần một bản ghi kỹ thuật Contact rõ nhãn. |
| `SEED_VISUAL_QA_DATA` | Không cần trong Docker Contact hiện tại | Template có key này nhưng Compose hiện không truyền biến đó vào API. Giữ `false`; không dựa vào nó để seed Contact. |

## File `.env` tối thiểu trên máy demo

Không copy nguyên giá trị mẫu. Tạo ba giá trị local riêng trước, sau đó điền theo cấu trúc sau:

```dotenv
MSSQL_SA_PASSWORD=<password-SQL-Server-local-manh>
SEED_ADMIN_PASSWORD=<password-admin-editor-local>
JWT_SIGNING_KEY=<chuoi-bi-mat-local-it-nhat-32-ky-tu>
NEWSDATA_API_KEY=
SEED_VISUAL_QA_DATA=false
SEED_TV3_LOCAL_DATA=false
```

Sau khi lưu `.env`, chạy `docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml config`. Lệnh này phải resolve được ba biến bắt buộc và không in warning biến rỗng. Chỉ sau đó mới chạy script demo.

## Lưu ý vận hành

Nếu trước đó đã chạy `CloudServiceStoreContactEmpty` với `SEED_ADMIN_PASSWORD` khác, account local trong database cũ không tự đổi password. Khi cần khởi tạo lại demo từ đầu, dùng `Run-ContactRequestEmptyDatabase.ps1 -Reset`; lệnh này chỉ reset compose project/volume database rỗng Contact theo đúng override.

Không thêm `ConnectionStrings__CloudServiceStore` vào `.env` trong luồng Compose chuẩn. API và migrator đã dùng hostname nội bộ `sqlserver,1433`; đưa `localhost` vào sẽ làm container kết nối sai chính nó.

## Biến môi trường frontend

Frontend có hai biến không bí mật khi chạy local: `API_BASE_URL` (mặc định `http://localhost:8080`) cho route proxy `/api/*`, và `NEXT_PUBLIC_SITE_URL` (mặc định `http://localhost:3000`) cho origin/metadata local. Chúng không cần nằm trong Docker `.env`; `Start-ContactRequestDemo.ps1` thiết lập chúng chỉ cho tiến trình `npm run dev`. Do đó `.env.example` hiện đủ cho backend Docker, còn frontend demo không cần secret hay file `.env` riêng.

## Kiểm tra source và frontend sau refactor

`admin-contact-requests-client.tsx` không còn khai báo inline bảng transition/status. Các metadata UX được đặt tại `frontend/src/lib/contact-request-status.ts`, có ghi rõ `ContactRequestService.CanTransition()` phía backend là nguồn workflow quyết định; API vẫn trả lỗi khi request vi phạm rule.

Kết quả chạy thật sau refactor: `npm run lint` pass. `npm run build` trong sandbox ban đầu bị ảnh hưởng bởi `NODE_ENV` không chuẩn và lỗi prerender `_global-error`; chạy lại bằng `NODE_ENV=production npm run build` đã pass, biên dịch TypeScript và tạo đủ 25 route, bao gồm `/contact` và `/admin/contact-requests`.
