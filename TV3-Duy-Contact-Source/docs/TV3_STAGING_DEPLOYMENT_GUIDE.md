# Hướng dẫn triển khai Staging — Contact Request TV3

## Mục đích và phạm vi

Tài liệu này mô tả triển khai **release candidate đã merge trên `dev`** lên môi trường **staging** để kiểm tra Contact Request trước PR release `dev` → `main`. Đây không phải hướng dẫn production và không thay thế runbook migration Contact-only, review CI hoặc approval của nhóm trưởng.

> **Ghi chú bắt buộc trước deploy:** `Cors:AllowedOrigins` trong shared config đang rỗng. Staging phải cung cấp một frontend origin chính xác qua `FRONTEND_STAGING_ORIGIN`, ví dụ `https://staging.example.com`; override sẽ map giá trị này thành `Cors__AllowedOrigins__0`. Nếu thiếu biến, Compose phải dừng sớm thay vì khởi động API với CORS sai. Đọc `TV3_STAGING_READINESS_AUDIT.md` trước khi deploy. Không coi bundle là staging đã pass khi migration Contact-only từ `dev` thật, Docker database rỗng, CI/review thật và smoke test staging chưa có evidence chạy thật.

> Compose gốc hiện phục vụ local demo: `ASPNETCORE_ENVIRONMENT` đang là `Development`, SQL Server publish cổng `1433`, và image SQL Server dùng tag `latest`. **Không dùng compose gốc trực tiếp cho staging** nếu chưa có override/hạ tầng được nhóm review.

## 1. Điều kiện đầu vào

| Điều kiện | Bắt buộc | Cách xác nhận |
|---|---:|---|
| Release candidate | Có | SHA trên `dev` có CI xanh và review thật theo quy trình nhóm. |
| Migration Contact-only | Có nếu schema Contact đổi | Sinh từ latest `dev`, review `Up/Down`, `Test-ContactRequestMigrationSafety.ps1` pass. |
| Backup/restore plan | Có | Có backup hoặc snapshot SQL Server staging và đã kiểm tra quy trình khôi phục. |
| Secret store | Có | Secrets nằm trong secret manager/CI protected variables; không commit `.env.staging`. |
| Image/version | Có | Ghi SHA Git, image tag/digest và thời gian triển khai trong change record. |
| Network | Có | SQL Server không public Internet; API chỉ mở qua reverse proxy/allowlist staging. |

## 2. Cấu hình môi trường staging

Tạo secrets trong hệ thống quản lý secret của staging. Không đưa giá trị vào source, screenshot, log CI, PR Description hoặc file `.env` được commit.

| Biến hoặc placeholder | Nơi tiêu thụ và quy tắc staging |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Bắt buộc `Staging`; compose gốc hard-code `Development` nên phải dùng override. |
| `MSSQL_SA_PASSWORD` | **Secret bắt buộc** cho `sqlserver`, API và migrator. Compose tự dựng `ConnectionStrings__CloudServiceStore` từ biến này; không cần, không nên tự đặt connection string thứ hai trong protected env file. Dùng password mạnh, riêng staging; không dùng password local/demo. |
| `JWT_SIGNING_KEY` | **Secret bắt buộc**; Compose map thành `Jwt__SigningKey`. Tối thiểu 32 ký tự, riêng staging, không tái sử dụng local/production. |
| `SEED_ADMIN_PASSWORD` | Secret **chỉ bắt buộc khi bootstrap database staging rỗng** để tạo account kỹ thuật hiện tại. Không cần đặt nếu database đã được quản lý/provision account theo quy trình khác. Rotate/quản lý account sau bootstrap; không chia sẻ trong tài liệu. |
| `SEED_TV3_LOCAL_DATA` | Alias tiện cho local `.env`. Operator staging **không cần đặt** biến này vì override ép trực tiếp `Seed__Tv3LocalDemoData=false`; không seed Contact QA/local ở staging. |
| `SEED_VISUAL_QA_DATA` | Alias tiện cho local `.env`. Operator staging **không cần đặt** biến này vì override ép trực tiếp `Seed__VisualQaData=false`; không tạo Visual QA data ở staging. |
| `NEWSDATA_API_KEY` | Chỉ cấp nếu staging cần kiểm thử News; không cần cho Contact. |
| `FRONTEND_STAGING_ORIGIN` | **Cấu hình bắt buộc, không phải secret**. Là exact browser origin của frontend staging, ví dụ `https://staging.example.com`. Không có path, slash cuối, wildcard hoặc danh sách cách nhau bằng dấu phẩy. Compose map giá trị này thành `Cors__AllowedOrigins__0` cho API; thiếu biến thì `docker compose up` phải dừng sớm. |
| `Cors__AllowedOrigins__0` | Biến .NET hiệu lực bên trong container API. **Không đặt độc lập** trong protected env file khi dùng override chuẩn; giá trị được lấy từ `FRONTEND_STAGING_ORIGIN`. Chỉ thêm `Cors__AllowedOrigins__1`, `__2` khi có origin bổ sung được review. |
| `API_BASE_URL` | Runtime frontend bắt buộc khi Next.js deploy tách API. Đây là URL **nội bộ** để server Next.js gọi API, ví dụ `http://api:8080` khi cùng Docker network; không dùng mặc định `http://localhost:8080` ở staging. |
| `NEXT_PUBLIC_SITE_URL` | Runtime frontend bắt buộc khi deploy tách API. Phải bằng đúng `FRONTEND_STAGING_ORIGIN` theo scheme, host và port; không thêm path/slash cuối. Proxy Next.js dùng giá trị này để gửi header `Origin` lên API. |
| `STAGING_ADMIN_ACCESS_TOKEN` | Token vận hành ngắn hạn, chỉ cấp qua protected session/environment ngay trước `Test-ContactRequestStagingSmoke.ps1`; bắt buộc cho nhánh smoke quản trị, không đưa vào source/PR/report. |
| `STAGING_CUSTOMER_ACCESS_TOKEN` | Token vận hành ngắn hạn **tùy chọn** cho assertion Customer trả `403` trong smoke script; không phải secret khởi động dịch vụ. |
| `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` | Connection string **test-only** cho script migration safety; không phải biến deploy staging và không dùng thay `MSSQL_SA_PASSWORD`. |

`DatabaseInitializer` có thể bootstrap `admin@cloud.local` và `editor@cloud.local` khi database chưa có user và có `SEED_ADMIN_PASSWORD`. Đây là hành vi bootstrap kỹ thuật hiện tại, **không phải mô hình account production**; team phải kiểm soát/rotate secret và không seed TV3 local data ở staging.

Frontend được triển khai tách API phải có cấu hình runtime riêng. `API_BASE_URL` phải là URL API **nội bộ** mà Next.js server truy cập được (ví dụ `http://api:8080` khi cùng Docker network); `NEXT_PUBLIC_SITE_URL` phải là origin public chính xác của frontend và phải cùng giá trị với `FRONTEND_STAGING_ORIGIN`. API proxy ghép thêm `/api/...` vào `API_BASE_URL` và chủ động gửi header `Origin` bằng `NEXT_PUBLIC_SITE_URL`, vì vậy không được để các giá trị mặc định `localhost` khi deploy staging. `next.config.ts` hiện chỉ khai báo `allowedDevOrigins` phục vụ HMR local; không phải cấu hình origin staging.

> **Quy tắc đối chiếu CORS trước `up`:** giá trị `FRONTEND_STAGING_ORIGIN`, `NEXT_PUBLIC_SITE_URL` và origin mà người dùng thực sự mở trên trình duyệt phải giống nhau về scheme, host và port. Chỉ `API_BASE_URL` được phép khác vì đó là endpoint nội bộ từ Next.js server tới API. Nếu frontend gọi API trực tiếp thay vì qua route proxy, vẫn phải giữ browser origin đó trong `Cors__AllowedOrigins__0`.

## 3. Override staging được review

Sử dụng `deploy/docker-compose.staging.yml.example` làm hunk/override tối thiểu để đặt API sang `Staging`, khóa TV3 local seed và yêu cầu một origin CORS frontend cụ thể. File này không thay thế firewall, reverse proxy, TLS hoặc cấu hình bỏ public SQL port; các phần đó do hạ tầng staging quản lý và phải được review riêng.

> `Cors:AllowedOrigins` trong shared `appsettings.json` là mảng rỗng. Nếu không inject `Cors__AllowedOrigins__0`, API không có origin frontend staging được phép. Override hiện dùng `${FRONTEND_STAGING_ORIGIN:?...}` để Docker dừng sớm khi đội triển khai quên cấu hình, thay vì khởi động API với CORS sai.

Trước khi start, kiểm tra YAML mà không nội suy secret:

```powershell
docker compose `
  -f docker-compose.yml `
  -f deploy/docker-compose.staging.yml.example `
  config --no-interpolate
```

Không đính kèm output `docker compose config` đã nội suy secret vào log/PR.

Trên host Linux/macOS/WSL có Bash, có thể thay bước kiểm tra thủ công bằng `scripts/Check-StagingCorsCompose.sh`; script xác nhận CORS origin/frontend runtime, secret cần thiết và chạy `docker compose config --no-interpolate`. Xem `TV3_STAGING_CORS_COMPOSE_PREFLIGHT.md` để dùng mà không lộ secret.

## 4. Trình tự triển khai staging

1. Checkout SHA release candidate trên `dev`; ghi SHA, image tag/digest và thời gian triển khai.
2. Xác minh migration Contact-only đã review và SQL staging có backup/snapshot. Nếu migration chưa đạt, dừng deployment.
3. Inject secrets qua secret manager hoặc file protected trên host staging, sau đó start bằng override:

```powershell
docker compose `
  --env-file <duong-dan-secret-protected> `
  -f docker-compose.yml `
  -f deploy/docker-compose.staging.yml.example `
  up --build --detach
```

Nếu frontend được vận hành tách compose hoặc tách host, cấu hình đồng thời `API_BASE_URL` và `NEXT_PUBLIC_SITE_URL` trong runtime frontend trước khi publish/restart frontend. Xác nhận `NEXT_PUBLIC_SITE_URL` bằng đúng `FRONTEND_STAGING_ORIGIN` đã dùng khi khởi động API. `STAGING_API_BASE_URL` trong lệnh health bên dưới chỉ là **placeholder tài liệu** cần thay bằng URL public của API; ứng dụng không tự đọc biến này.

4. Chờ `sqlserver` `healthy`, `migrator` `exited (0)`, và `api` `running`.
5. Kiểm tra health từ network staging được phép:

```powershell
docker compose -f docker-compose.yml -f deploy/docker-compose.staging.yml.example ps -a
docker compose -f docker-compose.yml -f deploy/docker-compose.staging.yml.example logs --no-color migrator
Invoke-WebRequest <STAGING_API_BASE_URL>/health -UseBasicParsing
```

## 5. Smoke test bắt buộc

| Luồng | Kỳ vọng |
|---|---|
| Health | HTTP `200`; SHA/image khớp release candidate nếu hạ tầng expose metadata. |
| Public Contact | Submit hợp lệ trả 201; validation sai trả `ProblemDetails` 400; duplicate trong cửa sổ 24 giờ trả 409. |
| Rate limit | Gửi hơn `RateLimiting:ContactPermitLimit` request hợp lệ với email unique từ cùng IP trong permit window; request bị chặn trả 429. Không tạo spam dữ liệu thật. |
| Authorization | Anonymous list trả 401; Customer trả 403; Admin/Editor list/detail/status đúng policy. |
| Workflow | `POST /api/v1/contact-requests/{id}/status`: Pending → Contacted → Approved hoặc Rejected/Cancelled có note; transition sai trả `ProblemDetails` 400/409 theo rule; terminal không reopen. |
| Frontend | `/contact` success/error; `/admin/contact-requests` paging/filter/history/status; mobile 360px không overflow. |
| Migration/schema | Hai bảng Contact, history/index/FK Contact tồn tại; không có schema module ngoài Contact từ migration TV3. |

Lưu link CI, SHA, `ps`, migrator log, health result, smoke-test record và ảnh browser nếu cần. Không lưu token/password/raw secret vào evidence.

Sau health hạ tầng, chạy `TV3_STAGING_SMOKE_TEST_GUIDE.md` để thực hiện smoke API có report JSON. Script yêu cầu `STAGING_ADMIN_ACCESS_TOKEN` từ session protected; không truyền token vào source hoặc PR. Probe rate limit là tùy chọn vì có thể ảnh hưởng cùng IP tester khác.

Trước và ngay sau deploy, dùng `TV3_STAGING_COMPOSE_ENVIRONMENT_ALIGNMENT.md` để đối chiếu biến override với guide, sau đó đi từng ô trong `TV3_STAGING_POST_DEPLOY_SMOKE_CHECKLIST.md`. Hai tài liệu này không thay thế evidence Docker/CI/migration/review thật.

## 6. Rollback và xử lý lỗi

Nếu migrator exit khác 0 hoặc health không đạt, **không retry mù**. Lưu `ps`, log `migrator`, log `sqlserver`, SHA image và rendered compose không chứa secret. Sửa nguyên nhân trên branch, review lại migration/diff, rồi triển khai release candidate mới. Không chạy `docker compose down -v` trên staging vì có thể xóa volume SQL Server.

Không tự động rollback EF migration bằng `Down()` hoặc `database update <migration-cu>` sau khi staging đã áp schema. Nếu migrator chưa áp thay đổi, dừng deployment và sửa release candidate. Nếu schema đã đổi một phần/toàn phần, ưu tiên **roll-forward migration** đã review; chỉ restore backup SQL khi change record/nhóm trưởng phê duyệt, dữ liệu ảnh hưởng đã được đánh giá và quy trình restore đã được kiểm thử. Chỉ rollback API image khi tương thích schema đã được xác nhận.

## 7. Cổng kết thúc staging

Chỉ đánh dấu staging đạt khi migration, health, smoke test Contact, frontend regression, CI SHA tương ứng và evidence đều pass. Sau đó nhóm trưởng mới quyết định PR release từ `dev` vào `main`; TV3 không push trực tiếp `main`.
