# Test nhanh API Contact Request — Rate Limit và Authorization

**Base URL trực tiếp tới Web API:** `http://localhost:8080/api/v1`
**Điều kiện trước khi test:** Docker demo đã đạt `/health` HTTP `200`. Dùng API trực tiếp ở cổng `8080`, không dùng URL frontend `3000` cho các lệnh curl bên dưới.

> Status Contact là enum số trong JSON: `1=Pending`, `2=Contacted`, `3=Approved`, `4=Rejected`, `5=Cancelled`. Backend `ContactRequestService` là nguồn xác thực workflow; các response `409` là kết quả đúng khi transition vi phạm rule.

## 1. Curl / PowerShell nhanh

Đặt biến dùng chung trong PowerShell:

```powershell
$ApiBase = "http://localhost:8080/api/v1"
$AdminEmail = "admin@cloud.local"
$AdminPassword = "<giá trị SEED_ADMIN_PASSWORD trong .env>"
```

| ID | Request | Cách chạy | Expected |
|---|---|---|---|
| API-01 | Public create hợp lệ | Gửi body có email duy nhất. | `201 Created`, body có `id`, status `1`. |
| API-02 | Admin list không token | `GET /contact-requests` không header Authorization. | `401 Unauthorized`. |
| API-03 | Admin list token sai | Gửi `Authorization: Bearer invalid-token`. | `401 Unauthorized`. |
| API-04 | Đăng nhập Admin | `POST /auth/login`; lấy `accessToken`. | `200 OK`, body có `accessToken`. |
| API-05 | Admin list token hợp lệ | `GET /contact-requests` với admin token. | `200 OK`. |
| API-06 | Customer list (tùy chọn) | Chỉ khi `SEED_TV3_LOCAL_DATA=true`; login `customer.qa@cloud.local`. | `403 Forbidden`. |
| API-07 | Burst public từ cùng IP | Chờ cửa sổ rate-limit mới, gửi 6 request hợp lệ email khác nhau liên tiếp. | Mặc định 5 request đầu `201`, request vượt permit limit `429`. |

### API-01 — public create và lưu `contactRequestId`

```powershell
$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$body = @{
  fullName = "QA Contact $stamp"
  email = "qa-contact-$stamp@example.test"
  phoneNumber = "0901234567"
  companyName = "QA Local"
  subject = "Kiểm thử Contact API"
  message = "Dữ liệu kỹ thuật hợp lệ để kiểm tra API Contact Request."
} | ConvertTo-Json

$created = Invoke-RestMethod "$ApiBase/contact-requests" -Method Post -ContentType "application/json" -Body $body
$ContactRequestId = $created.id
$created
```

### API-02 / API-03 — chặn truy cập quản trị

```powershell
curl.exe -i "$ApiBase/contact-requests?page=1&pageSize=20"
curl.exe -i -H "Authorization: Bearer invalid-token" "$ApiBase/contact-requests?page=1&pageSize=20"
```

### API-04 / API-05 — login và list với Admin token

```powershell
$loginBody = @{ email = $AdminEmail; password = $AdminPassword } | ConvertTo-Json
$login = Invoke-RestMethod "$ApiBase/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
$AdminToken = $login.accessToken

curl.exe -i -H "Authorization: Bearer $AdminToken" "$ApiBase/contact-requests?page=1&pageSize=20"
```

### API-06 — xác nhận Customer bị `403` (tùy chọn)

Chỉ chạy nếu demo được khởi động với `SEED_TV3_LOCAL_DATA=true`; account customer kỹ thuật được seed với cùng password `SEED_ADMIN_PASSWORD`.

```powershell
$customerLoginBody = @{ email = "customer.qa@cloud.local"; password = $AdminPassword } | ConvertTo-Json
$customerLogin = Invoke-RestMethod "$ApiBase/auth/login" -Method Post -ContentType "application/json" -Body $customerLoginBody
curl.exe -i -H "Authorization: Bearer $($customerLogin.accessToken)" "$ApiBase/contact-requests?page=1&pageSize=20"
```

### API-07 — rate limit theo IP, expected `429`

Rate limit mặc định là `RateLimiting:ContactPermitLimit=5` trong một `WindowSeconds=60`, tính theo IP. Không chạy request này trên môi trường dùng chung. Chờ ít nhất 61 giây sau các public POST trước đó để cửa sổ mới bắt đầu.

```powershell
Start-Sleep -Seconds 61
1..6 | ForEach-Object {
  $stamp = "$(Get-Date -Format 'yyyyMMddHHmmssfff')-$($_)"
  $burstBody = @{
    fullName = "Rate Limit QA $($_)"
    email = "rate-limit-$stamp@example.test"
    phoneNumber = "0901234567"
    subject = "Rate limit $($_)"
    message = "Payload kỹ thuật hợp lệ để kiểm tra rate limit theo IP."
  } | ConvertTo-Json -Compress

  $status = curl.exe -sS -o "$env:TEMP\contact-rate-$_.json" -w "%{http_code}" `
    -H "Content-Type: application/json" -d $burstBody "$ApiBase/contact-requests"
  "Request $($_): HTTP $status"
}
```

Nếu đã có request public khác trong cùng cửa sổ, `429` có thể xuất hiện sớm hơn request thứ sáu; đây vẫn là behavior đúng. Sau `429`, chờ hết 60 giây rồi gửi request hợp lệ mới để xác nhận limiter tự cấp lại permit.

## 2. Postman

Import hai file có sẵn:

```text
tests/postman/CloudServiceStore_TV3.postman_collection.json
tests/postman/CloudServiceStore_TV3_Local.postman_environment.json
```

Chọn environment **CloudServiceStore TV3 Local**, rồi chỉ điền `adminPassword` bằng giá trị `SEED_ADMIN_PASSWORD` local; không export environment có password. Chạy theo thứ tự: **Đăng nhập Admin** → **Public submit contact** → **Admin/Editor list contacts** → **Admin/Editor contact detail/history**. Để test rate limit, dùng body email khác nhau và gửi lặp request `Rate-limit contact` từ cùng máy cho tới `429`.

## 3. Lưu ý khi đánh giá kết quả

`401` chứng minh thiếu/sai token; `403` chỉ dùng để chứng minh token hợp lệ nhưng role không có quyền. `409` cho duplicate window hoặc workflow invalid không phải lỗi server. Không test `429` bằng cách gửi lặp cùng email, vì duplicate `409` có thể che kết quả limiter.
