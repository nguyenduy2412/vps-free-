# Chạy smoke test staging — Contact Request TV3

Script `scripts/Test-ContactRequestStagingSmoke.ps1` kiểm tra API Contact sau khi staging đã đạt điều kiện hạ tầng: SQL healthy, migrator `exited (0)`, API `/health` 200 và release candidate có SHA/CI đã ghi nhận.

## Chuẩn bị secret session

Không ghi token vào source, file `.env`, command history, PR hoặc evidence. Tạo access token Admin từ luồng staging được kiểm soát, đặt token vào environment của **session PowerShell hiện tại**; Customer token là tùy chọn để xác nhận `403`.

```powershell
$env:STAGING_ADMIN_ACCESS_TOKEN = '<admin-access-token-tu-secure-session>'
$env:STAGING_CUSTOMER_ACCESS_TOKEN = '<optional-customer-access-token>'
```

## Chạy smoke test mặc định

```powershell
.\scripts\Test-ContactRequestStagingSmoke.ps1 `
  -ApiBaseUrl 'https://staging-api.example.com'
```

Script tạo một Contact kỹ thuật với email `@example.test`, sau đó kiểm tra health, public create 201, duplicate 409, validation 400, anonymous 401, optional Customer 403, Admin paging/status-filter/detail/history 200, missing resource 404, enum/note workflow invalid 400, workflow `Pending → Contacted → Approved` và terminal reopen 409. Report JSON không chứa token/Authorization header, nằm tại `artifacts/staging-smoke-contact/`.

## Kiểm tra rate limit có chủ đích

Chỉ chạy khi cửa sổ rate limit staging không ảnh hưởng tester khác. Đặt `RateLimitProbeCount` lớn hơn `RateLimiting:ContactPermitLimit` của staging; script dùng email kỹ thuật unique để tránh duplicate 409 che kết quả 429.

```powershell
.\scripts\Test-ContactRequestStagingSmoke.ps1 `
  -ApiBaseUrl 'https://staging-api.example.com' `
  -IncludeRateLimitProbe `
  -RateLimitProbeCount 6
```

Nếu probe không thấy 429, không tự kết luận policy lỗi: xác nhận limit staging và window trước, rồi chạy lại ở khoảng thời gian được team đồng ý.

## Evidence và cleanup

Lưu report JSON, SHA release candidate, migrator log, API health result và screenshot frontend nếu có yêu cầu hồ sơ. Bản ghi Contact kỹ thuật được gắn `TV3 Staging`/`example.test`; team có thể xóa qua quy trình quản trị sau khi evidence được chấp nhận. Không xóa data staging trực tiếp bằng database query không được review.
