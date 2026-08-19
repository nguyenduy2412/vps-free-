# Đối chiếu biến môi trường Compose với guide staging — TV3 Contact Request

> **Kết luận kiểm tra tĩnh:** không có file tên `docker-compose.override.yml` trong source hiện tại. File override staging tương đương và phải dùng cho TV3 là `deploy/docker-compose.staging.yml.example`. Các biến CORS/seed trong file này **khớp** với quy tắc mới trong `TV3_STAGING_DEPLOYMENT_GUIDE.md`. Đây không phải bằng chứng Docker đã render hoặc container đã khởi động.

## 1. File Compose được phép dùng cho staging

| File | Vai trò | Dùng trong staging TV3? | Lý do |
|---|---|---:|---|
| `docker-compose.yml` | Compose nền: `sqlserver`, `migrator`, `api`. | Có, nhưng phải kết hợp override staging. | Chứa connection string dựng từ password, JWT, service dependency và migrator. |
| `deploy/docker-compose.staging.yml.example` | Override staging TV3 canonical. | **Có**. | Ép `Staging`, tắt hai loại seed và inject CORS origin bắt buộc. |
| `docker-compose.contact-empty.yml` | Override database rỗng phục vụ demo/migration test. | Không dùng cho deploy staging thông thường. | Chỉ đổi database sang `CloudServiceStoreContactEmpty`. |
| `docker-compose.visual-qa.yml` | Môi trường Visual QA local độc lập. | **Không dùng**. | Đặt `Development`, `Seed__VisualQaData=true`, CORS localhost và rate-limit QA; trái quy tắc staging. |

Lệnh staging chuẩn là:

```powershell
docker compose `
  --env-file <duong-dan-protected> `
  -f docker-compose.yml `
  -f deploy/docker-compose.staging.yml.example `
  up --build --detach
```

## 2. Ma trận khớp biến môi trường

| Yêu cầu trong guide | Nơi cấu hình thực tế | Trạng thái | Diễn giải vận hành |
|---|---|---:|---|
| API chạy `Staging` | `ASPNETCORE_ENVIRONMENT: Staging` trong override staging. | Khớp | Ghi đè `Development` ở compose nền. |
| Không seed Contact local | `Seed__Tv3LocalDemoData: "false"` trong override staging. | Khớp | Override .NET trực tiếp, không phụ thuộc alias local `SEED_TV3_LOCAL_DATA`. |
| Không seed Visual QA | `Seed__VisualQaData: "false"` trong override staging. | Khớp | Chặn rõ môi trường Visual QA/local; không phụ thuộc alias local `SEED_VISUAL_QA_DATA`. |
| Chỉ cho phép frontend origin staging | `Cors__AllowedOrigins__0: ${FRONTEND_STAGING_ORIGIN:?...}` trong override staging. | Khớp | Thiếu `FRONTEND_STAGING_ORIGIN` làm Compose dừng trước start; giá trị phải là one exact origin. |
| `MSSQL_SA_PASSWORD` cho SQL/API/migrator | Compose nền tiêu thụ và tự dựng connection string. | Khớp có điều kiện | Operator phải inject secret; không tự thêm `ConnectionStrings__CloudServiceStore` thứ hai. |
| JWT key riêng staging | Compose nền map `JWT_SIGNING_KEY` sang `Jwt__SigningKey`. | Khớp có điều kiện | Operator phải inject secret tối thiểu 32 ký tự. |
| Bootstrap admin khi cần | Compose nền map `SEED_ADMIN_PASSWORD` sang `Seed__AdminPassword`. | Khớp có điều kiện | Chỉ cần khi database staging rỗng và cần bootstrap account kỹ thuật. |
| News không thuộc Contact | Compose nền map `NEWSDATA_API_KEY`. | Khớp | Có thể để trống khi staging không kiểm thử News. |
| Next.js proxy gọi API đúng | Runtime frontend đọc `API_BASE_URL`, `NEXT_PUBLIC_SITE_URL`. | Ngoài Compose API | Infra frontend phải cấu hình riêng: `NEXT_PUBLIC_SITE_URL = FRONTEND_STAGING_ORIGIN`; `API_BASE_URL` là URL nội bộ server-to-server. |

## 3. Điều kiện CORS phải xác nhận trước start

Trước lệnh `up`, operator phải đặt **một** giá trị không nhạy cảm trong protected environment file hoặc secret manager:

```text
FRONTEND_STAGING_ORIGIN=https://staging.example.com
```

Ba giá trị sau phải nhất quán về scheme, host và port:

| Giá trị | Ví dụ đúng | Không hợp lệ |
|---|---|---|
| `FRONTEND_STAGING_ORIGIN` | `https://staging.example.com` | Có path, slash cuối, wildcard hoặc nhiều origin trong một biến. |
| `Cors__AllowedOrigins__0` hiệu lực trong API | Được Compose suy ra từ giá trị trên. | Đặt độc lập một giá trị khác khi vẫn dùng override chuẩn. |
| `NEXT_PUBLIC_SITE_URL` ở frontend | `https://staging.example.com` | `http://localhost:3000`, domain/port khác frontend thật, hoặc path. |

`API_BASE_URL` là ngoại lệ hợp lệ: đó là URL nội bộ mà Next.js server gọi, ví dụ `http://api:8080`; không phải browser origin và không được ghi vào CORS allowlist thay frontend public origin.

## 4. Cổng còn chưa thể xác nhận bằng kiểm tra tĩnh

Không kết luận staging pass chỉ vì biến khớp. Trước release phải có evidence: Compose render với `--no-interpolate`, container `sqlserver` healthy, migrator exit `0`, API health `200`, CORS preflight, smoke script, kiểm tra frontend, migration Contact-only từ `dev` thật, CI đúng SHA và cross-review thật.
