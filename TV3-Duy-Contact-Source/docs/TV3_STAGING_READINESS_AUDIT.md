# Audit sẵn sàng Staging — Bundle TV3 Contact Request v43

> **Kết luận ngắn:** bundle v43 đã đạt mức **sẵn sàng cấu hình có điều kiện** sau khi bổ sung override CORS và khóa seed Visual QA. Đây là kết quả kiểm tra tĩnh file cấu hình, script và tài liệu; **không phải** xác nhận triển khai staging đã thành công. Docker, migration Contact sinh từ `dev` thật, CI, cross-review và smoke test staging vẫn chưa có bằng chứng chạy thật.

> **Ghi chú vận hành cần đọc trước deploy:** shared `Cors:AllowedOrigins` hiện rỗng. Operator phải đặt một origin frontend staging chính xác, ví dụ `FRONTEND_STAGING_ORIGIN=https://staging.example.com`. Override map biến này thành `Cors__AllowedOrigins__0` và dùng cú pháp biến bắt buộc để Compose dừng sớm nếu thiếu. Sau phần cấu hình, vẫn phải có bằng chứng migration Contact-only sinh từ `dev` thật, Docker database rỗng, CI/review thật và smoke test staging; audit này không thay thế các cổng đó.

## 1. Phạm vi, phương pháp và bằng chứng

Audit này chỉ đánh giá source TV3/Duy thuộc `feature/contact-request-management`, đặc biệt là các biến môi trường được Docker/API/frontend/script sử dụng. Không có secret thật nào được đọc, ghi, in ra hoặc đưa vào báo cáo.

| Hạng mục đã kiểm tra tĩnh | Kết quả | Bằng chứng trong workspace |
|---|---|---|
| ZIP v43 không hỏng dữ liệu nén | Đạt | `unzip -t` báo *No errors detected*. |
| Định danh archive | Đạt | SHA-256: `7f57517cc23300bb0b0271a3d351ba9a9d916948537c5122f80490db88183ee5`. |
| Template secret local | Đạt có điều kiện | `.env.example` dùng placeholder; không được copy thành `.env.staging` hoặc commit secret. |
| Override staging TV3 | Đã cập nhật | `deploy/docker-compose.staging.yml.example` đặt `Staging`, khóa hai seed và bắt buộc CORS origin. |
| Hướng dẫn deploy/smoke/rollback | Đã cập nhật | `TV3_STAGING_DEPLOYMENT_GUIDE.md` và `TV3_STAGING_SMOKE_TEST_GUIDE.md`. |
| Docker render/start | Chưa xác minh | Sandbox không có Docker CLI/daemon; không được suy diễn là compose chạy được. |

Archive có chủ đích không thay thế các file dùng chung như `docker-compose.yml`, `appsettings.json` và `frontend/next.config.ts`. Đây là đúng ranh giới **TV3-only**, nhưng đội tích hợp phải merge từng hunk vào clone `dev` theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`, rồi kiểm tra lại trên source `dev` thực tế.

## 2. Bản đồ cấu hình và ownership

Compose gốc tạo ba service `sqlserver`, `migrator` và `api`. `migrator` dùng connection string riêng từ `MSSQL_SA_PASSWORD`; API dùng cùng password để kết nối SQL Server, `JWT_SIGNING_KEY` cho JWT và `SEED_ADMIN_PASSWORD` cho bootstrap có điều kiện. Frontend không thuộc compose gốc, nhưng API proxy Next.js đọc `API_BASE_URL` và `NEXT_PUBLIC_SITE_URL` khi chạy ở runtime.

| Nhóm | File/cơ chế nguồn | Vai trò staging | Trạng thái audit |
|---|---|---|---|
| Database | `docker-compose.yml` + protected environment | Khởi tạo SQL Server và connection string cho API/migrator. | Cần secret staging thật và kiểm chứng container. |
| API | `appsettings.json`, Compose và override staging | CORS, rate limit Contact, seed guard, JWT, connection string. | CORS/seed đã có override; shared file phải được merge đúng hunk. |
| Frontend Next.js | `frontend/src/app/api/[...path]/route.ts` | Proxy `/api/*` sang API và gửi `Origin` của frontend. | Cần cấu hình runtime frontend riêng, không do compose TV3 quản lý. |
| Smoke test | `Test-ContactRequestStagingSmoke.ps1` | Kiểm tra API sau deploy bằng token ngắn hạn/protected. | Không được đặt token trong `.env.example`, source hay PR. |

## 3. Ma trận biến môi trường và cấu hình

| Biến hoặc khóa | Nơi tiêu thụ thực tế | Phân loại | Quy tắc staging | Trạng thái |
|---|---|---|---|---|
| `MSSQL_SA_PASSWORD` | `sqlserver`, connection string API, connection string migrator | **Secret bắt buộc** | Password SQL Server mạnh, riêng staging; inject qua secret manager hoặc file protected trên host. | Chưa có giá trị thật; đúng chủ đích. |
| `JWT_SIGNING_KEY` | `Jwt__SigningKey` trong API | **Secret bắt buộc** | Tối thiểu 32 ký tự, riêng staging, không tái sử dụng local/production. | Chưa có giá trị thật; đúng chủ đích. |
| `SEED_ADMIN_PASSWORD` | `Seed__AdminPassword` trong API | Secret có điều kiện | Chỉ cấp lúc bootstrap database staging rỗng; quản lý/rotate account sau bootstrap. | Chưa có giá trị thật; đúng chủ đích. |
| `ASPNETCORE_ENVIRONMENT` | API | Cấu hình bắt buộc | Phải là `Staging`; compose gốc đang là `Development`. | Override staging đã đặt `Staging`. |
| `FRONTEND_STAGING_ORIGIN` | Nội suy Compose thành `Cors__AllowedOrigins__0` | Cấu hình bắt buộc | Một exact origin HTTPS, ví dụ `https://staging.example.com`; không path, slash cuối, wildcard hay danh sách nhiều origin. | **Đã thêm**; Compose dừng sớm nếu thiếu. |
| `Cors__AllowedOrigins__0` | `Program.cs` đọc `Cors:AllowedOrigins` | Cấu hình API bắt buộc | Phải nhận từ `FRONTEND_STAGING_ORIGIN`; bổ sung index `__1`, `__2` chỉ khi có origin chính đáng được review. | **Đã thêm** vào override. |
| `SEED_TV3_LOCAL_DATA` / `Seed__Tv3LocalDemoData` | Compose gốc / `Program.cs` | Cấu hình an toàn dữ liệu | Không seed Contact local trên staging. | Override ép `Seed__Tv3LocalDemoData=false`. |
| `SEED_VISUAL_QA_DATA` / `Seed__VisualQaData` | `.env.example`, `Program.cs` | Cấu hình an toàn dữ liệu | Không tạo Visual QA data trên staging. | Override ép `Seed__VisualQaData=false`. |
| `API_BASE_URL` | Next.js route proxy, server-side | Cấu hình frontend bắt buộc khi tách deploy | URL nội bộ API mà Next server truy cập được, chẳng hạn `http://api:8080` khi cùng network; không mặc định `localhost`. | Cần do frontend runtime/infra cung cấp. |
| `NEXT_PUBLIC_SITE_URL` | Next.js route proxy, server-side | Cấu hình frontend bắt buộc khi tách deploy | Phải bằng exact public origin đã đặt trong `FRONTEND_STAGING_ORIGIN`. | Cần do frontend runtime/infra cung cấp. |
| `NEWSDATA_API_KEY` | Compose/API, không cần cho Contact | Secret tùy chọn | Bỏ trống nếu staging không kiểm thử News; không tác động phạm vi Contact TV3. | Không chặn Contact deploy. |
| `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` | Script migration safety/test chuyên dụng | Test-only secret | Chỉ dùng môi trường test được phép; không dùng làm connection string deployment. | Không chặn staging deploy. |
| `STAGING_ADMIN_ACCESS_TOKEN` | Smoke script | Token vận hành ngắn hạn | Cấp qua session/secret protected ngay trước smoke test; script không ghi token vào report. | Bắt buộc để chạy smoke quản trị. |
| `STAGING_CUSTOMER_ACCESS_TOKEN` | Smoke script | Token vận hành tùy chọn | Dùng để kiểm tra nhánh `403 Customer`; không cần để chạy các check cốt lõi khác. | Tùy chọn. |
| `RateLimiting:ContactPermitLimit` | `appsettings.json`, rate-limit Contact | Khóa cấu hình API | Baseline hiện là `5` request/cửa sổ. Nếu staging override thay đổi, `RateLimitProbeCount` phải lớn hơn permit limit. | Cần đọc giá trị sau deploy trước probe. |

`ACCEPT_EULA=Y` là giá trị tĩnh trong service SQL Server, không phải secret. Các biến seed có tên `SEED_*` trong `.env.example` là thuận tiện cho local demo; staging phải ưu tiên override `.NET` rõ ràng như `Seed__Tv3LocalDemoData=false` và `Seed__VisualQaData=false`, không dựa vào giá trị mặc định hoặc vào file `.env` không được quản lý.

## 4. Phát hiện quan trọng và thay đổi khắc phục

### CORS frontend staging trước đây chưa được inject

Shared `appsettings.json` đặt `Cors:AllowedOrigins` là mảng rỗng. `Program.cs` đọc chính khóa này để tạo policy `Frontend`, trong khi Next.js proxy đặt header `Origin` từ `NEXT_PUBLIC_SITE_URL`. Nếu staging không inject origin cụ thể, request proxy/browser có thể bị từ chối CORS hoặc khởi động với policy không đúng mục tiêu triển khai.

Override staging nay có dòng sau:

```yaml
Cors__AllowedOrigins__0: ${FRONTEND_STAGING_ORIGIN:?Set FRONTEND_STAGING_ORIGIN to the exact public frontend origin}
```

Biến Compose có cú pháp bắt buộc giúp dừng lệnh `up` khi operator quên cấu hình origin. Nó không lưu origin vào source secret; origin là cấu hình không nhạy cảm nhưng phải được quản lý cùng release record. Trước deploy, đặt một giá trị duy nhất trong môi trường protected, ví dụ:

```text
FRONTEND_STAGING_ORIGIN=https://staging.example.com
```

Không dùng `*`, `https://staging.example.com/`, `https://staging.example.com/contact`, hoặc `https://a.example.com,https://b.example.com`. Nếu cần nhiều origin, bổ sung có review `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2` trong override riêng của môi trường.

### Frontend tách deploy chưa có Compose service trong bundle TV3

`API_BASE_URL` và `NEXT_PUBLIC_SITE_URL` đã được source frontend tiêu thụ nhưng compose nền không quản lý service Next.js. Đây không phải thiếu sót phạm vi TV3-only; tuy nhiên infra owner phải cung cấp hai biến này trong frontend runtime/build pipeline. Đặc biệt, `NEXT_PUBLIC_SITE_URL` phải khớp từng ký tự (scheme, host, port) với `FRONTEND_STAGING_ORIGIN` của API.

`next.config.ts` chỉ có `allowedDevOrigins` phục vụ HMR local. Không sử dụng danh sách này để kết luận origin staging đã cấu hình.

## 5. Quy trình cấu hình staging tối thiểu

Trên host staging hoặc trong secret manager/CI protected variables, cung cấp `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, `SEED_ADMIN_PASSWORD` (nếu bootstrap cần), và `FRONTEND_STAGING_ORIGIN`. Không commit file giá trị thật. Sau đó, render compose trước mà không in giá trị resolved vào evidence:

```powershell
docker compose `
  -f docker-compose.yml `
  -f deploy/docker-compose.staging.yml.example `
  config --no-interpolate
```

Chỉ sau khi release candidate có migration Contact-only đã review, CI xanh, backup/snapshot SQL staging và change record đầy đủ mới được chạy `up`. Sau health, xác nhận `migrator` exit `0` và chạy script smoke với `STAGING_ADMIN_ACCESS_TOKEN` từ protected session. Quy trình đầy đủ, rollback và evidence nằm trong `TV3_STAGING_DEPLOYMENT_GUIDE.md`.

## 6. Hạng mục không thể xác nhận từ bundle

| Hạng mục | Trạng thái | Lý do và hành động trước deploy |
|---|---|---|
| Migration chỉ Contact | **Blocked** | Phải sinh lại bằng `dotnet ef migrations add` trên clone `dev` thật, rồi review `Up/Down`. Không dùng migration thủ công/snapshot cũ. |
| Docker SQL Server/migrator/API | **Chưa có evidence** | Sandbox không có Docker CLI/daemon; chạy compose DB rỗng trên host staging/local có Docker và lưu `ps`, exit migrator, health. |
| CI đúng SHA release candidate | **Chưa có evidence** | Cần build/test backend, lint/build frontend trên branch/PR thật. |
| Cross-review TV2 | **Chưa có evidence** | Cần review collaborator thật trên PR mới; không tự xác nhận bằng bundle. |
| Cổng mạng/TLS/SQL public port | **Chưa có evidence** | Compose gốc có publish `1433:1433` và image SQL Server `2022-latest`; infra owner phải kiểm soát firewall/reverse proxy/TLS và pin/review image theo chính sách đội. |
| Frontend runtime env | **Chưa có evidence** | Infra frontend phải set `API_BASE_URL` và `NEXT_PUBLIC_SITE_URL`, sau đó kiểm tra `/contact` và `/admin/contact-requests`. |

## 7. Cổng quyết định

Không gắn nhãn **“staging đã pass”** chỉ từ audit này. Chỉ có thể chuyển trạng thái thành pass khi tất cả điều kiện sau có evidence thật: migration Contact-only, render/start Compose, migrator `0`, API health `200`, smoke test API/frontend, CI đúng SHA và cross-review theo quy trình nhóm. Cho tới lúc đó, trạng thái chính xác là **cấu hình bundle đã được rà soát và còn các blocker hạ tầng/release cần xử lý**.
