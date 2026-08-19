# Chạy demo Docker Contact Request TV3

Tài liệu này dùng cho **clone `dev` thật** đã merge đúng 22 file Contact TV3 và đã sinh migration Contact-only bằng `dotnet ef`. Không dùng để áp migration cũ hoặc migration có bảng của module khác.

## 1. Chuẩn bị `.env`

Tại thư mục gốc, tạo `.env` từ `.env.example` nếu chưa có. Giá trị tối thiểu cần tồn tại là `MSSQL_SA_PASSWORD`, `SEED_ADMIN_PASSWORD` và `JWT_SIGNING_KEY`. Không commit `.env`.

```powershell
Copy-Item .env.example .env
notepad .env
```

Mật khẩu SQL Server phải đủ mạnh. Khi demo local, dùng giá trị riêng của máy; không dùng mật khẩu mẫu trong tài liệu hay gửi lên GitHub.

| Biến trong `.env.example` | Mức cần thiết cho demo Contact | Cách dùng trong Docker hiện tại |
|---|---|---|
| `MSSQL_SA_PASSWORD` | **Bắt buộc** | SQL Server và connection string của API/migrator. Phải thay placeholder, đủ mạnh; không bọc giá trị trong dấu ngoặc kép trừ khi password thực sự cần chúng. |
| `SEED_ADMIN_PASSWORD` | **Bắt buộc nếu demo Admin/Editor** | API dùng để seed hai account local `admin@cloud.local` và `editor@cloud.local` ở lần database rỗng đầu tiên. Phải thay placeholder. |
| `JWT_SIGNING_KEY` | **Bắt buộc** | API dùng để ký JWT; thay placeholder bằng một chuỗi local riêng dài ít nhất 32 ký tự. |
| `NEWSDATA_API_KEY` | Tùy chọn | Có thể để trống khi demo Contact, vì không thuộc luồng Contact Request. |
| `SEED_TV3_LOCAL_DATA` | Tùy chọn | Mặc định `false`. Chỉ đặt `true` khi muốn seed một bản ghi Contact kỹ thuật và account Customer QA local có nhãn rõ ràng. |
| `SEED_VISUAL_QA_DATA` | Không cần cho Docker Contact hiện tại | Có trong template `.env.example` nhưng hiện không được Docker Compose truyền vào API. Để `false`; không dùng nó để kỳ vọng có seed Contact. |

Không thêm `ConnectionStrings__CloudServiceStore` vào `.env` cho demo Compose trừ khi nhóm chủ động thay đổi Compose: API và migrator đã nhận connection string nội bộ `sqlserver,1433`. Không cần biến URL frontend để chạy script Docker này.

## 2. Kiểm tra Compose trước khi chạy

Mở PowerShell ở thư mục chứa `docker-compose.yml`, rồi kiểm tra Docker Desktop đang chạy và các biến môi trường đã được resolve:

```powershell
docker compose version
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml config
```

Lệnh `config` phải hoàn tất mà không có biến rỗng hoặc lỗi parse YAML. Override `contact-empty` dùng database `CloudServiceStoreContactEmpty` và volume riêng, không đụng volume SQL Server demo chính.

## 3. Chạy database rỗng và migrator

Chạy script đã được cập nhật. Script sẽ chờ tối đa 180 giây để migrator chuyển sang trạng thái `exited`, chỉ chấp nhận exit code `0`, sau đó mới đợi API `/health` trả `200`.

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-ContactRequestEmptyDatabase.ps1
```

Đối với lần demo cần xóa riêng database rỗng Contact trước đó, dùng:

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
```

> `-Reset` chỉ xóa resources của compose project `cloudservicestore_contact_empty`. Không dùng `docker compose down -v` cho compose chính nếu còn dữ liệu nhóm cần giữ.

## 4. Điều kiện demo đạt

Kiểm tra lại trạng thái và log ngay sau script:

```powershell
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

| Service | Kết quả cần có |
|---|---|
| `sqlserver` | `running` và `healthy` |
| `migrator` | `exited (0)` |
| `api` | `running` |
| `GET /health` | HTTP `200` |

Sau đó mới demo API/UI Contact. Kiểm tra form public tại `/contact`; trang Admin/Editor tại `/admin/contact-requests` cần chạy frontend Next.js theo README của repository.

## 5. Khởi động frontend cho trình duyệt

Script `Run-ContactRequestEmptyDatabase.ps1 -Reset` đã tự gọi `docker compose ... up --build --detach`, chờ migrator exit `0` và kiểm tra API health. Vì vậy **không chạy lại** `docker compose ... up -d --build` ngay sau script, trừ khi script đã thất bại hoặc bạn đã chủ động dừng service.

Docker Compose hiện chỉ khởi động SQL Server, migrator và Web API trên cổng `8080`; frontend Next.js phải chạy riêng trên cổng `3000`:

```powershell
cd frontend
npm ci
$env:API_BASE_URL = "http://localhost:8080"
$env:NEXT_PUBLIC_SITE_URL = "http://localhost:3000"
npm run dev
```

Giữ terminal này mở, rồi truy cập `http://localhost:3000/contact` để gửi yêu cầu public. Để demo quản trị, truy cập `http://localhost:3000/login`, đăng nhập `admin@cloud.local` với `SEED_ADMIN_PASSWORD`, sau đó mở `http://localhost:3000/admin/contact-requests`.

> `API_BASE_URL` và `NEXT_PUBLIC_SITE_URL` chỉ là biến môi trường của tiến trình Next.js local; không cần thêm vào `.env` Docker và không phải secret. API proxy `frontend/src/app/api/[...path]/route.ts` sẽ chuyển request `/api/*` từ cổng 3000 sang API Docker ở cổng 8080.

### Lệnh một lần thay cho các bước Docker + frontend

Để chạy tất cả phần demo trên máy Windows có Docker Desktop, PowerShell và Node.js, dùng script mới tại thư mục gốc:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Start-ContactRequestDemo.ps1 -ResetDatabase
```

Script sẽ kiểm tra `.env`, validate Compose, reset volume Contact rỗng, chờ migrator/API health, chạy `npm ci` và khởi động frontend Next.js. Khi xong, terminal in URL public/login/admin và process id frontend. Các lần chạy sau có thể bỏ `-ResetDatabase` để giữ database demo; dùng `-SkipNpmInstall` khi dependencies đã sẵn sàng. Nếu muốn quan sát log frontend trực tiếp trong terminal hiện tại, thêm `-ForegroundFrontend`.

## 6. Nếu script báo lỗi

Không chạy lại mù. Lấy đủ ba nguồn evidence sau trước:

```powershell
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color sqlserver
```

Nếu migrator exit khác `0`, kiểm tra trước migration Contact-only, `.env`, connection string và `dotnet-ef` build log. Nếu SQL Server `unhealthy`, kiểm tra Docker Desktop, password SQL và SQL Server log. Không sửa tay migration để che model drift.
