# Checklist smoke test ngay sau deploy staging — TV3 Contact Request

> Chỉ bắt đầu checklist này sau khi release candidate đã qua cổng migration Contact-only, CI/review và Compose đã được khởi động trên hạ tầng staging. Mỗi ô chỉ được đánh dấu khi có evidence thật; checklist không thay thế `TV3_STAGING_SMOKE_TEST_GUIDE.md` hoặc script smoke.

## A. Xác nhận hạ tầng và release identity

- [ ] Ghi SHA commit `dev`, image tag/digest, thời điểm deploy và URL staging vào change record; không ghi secret/token.
- [ ] Render lại cấu hình mà không lộ secret:

  ```powershell
  docker compose -f docker-compose.yml -f deploy/docker-compose.staging.yml.example config --no-interpolate
  ```

  Kỳ vọng: override `ASPNETCORE_ENVIRONMENT=Staging`, hai seed `false` và `Cors__AllowedOrigins__0` xuất hiện; không có output secret đã resolved trong evidence.

- [ ] Kiểm tra trạng thái service:

  ```powershell
  docker compose -f docker-compose.yml -f deploy/docker-compose.staging.yml.example ps -a
  ```

  Kỳ vọng: `sqlserver` là `healthy`, `migrator` là `exited (0)` và `api` là `running`.

- [ ] Lưu log migrator không có secret:

  ```powershell
  docker compose -f docker-compose.yml -f deploy/docker-compose.staging.yml.example logs --no-color migrator
  ```

  Kỳ vọng: không có exception migration; chỉ migration Contact đã review được áp dụng.

- [ ] Kiểm tra health từ network được phép:

  ```powershell
  Invoke-WebRequest <STAGING_API_BASE_URL>/health -UseBasicParsing
  ```

  Kỳ vọng: HTTP `200`.

## B. CORS và frontend runtime

- [ ] Xác nhận `FRONTEND_STAGING_ORIGIN` và `NEXT_PUBLIC_SITE_URL` cùng scheme, host, port với URL frontend người dùng thực sự mở; `API_BASE_URL` phải là URL nội bộ của API từ Next.js server.
- [ ] Chạy CORS preflight từ máy được phép, thay origin bằng giá trị staging thật:

  ```powershell
  curl.exe -i -X OPTIONS "<STAGING_API_BASE_URL>/api/v1/contact-requests" `
    -H "Origin: <FRONTEND_STAGING_ORIGIN>" `
    -H "Access-Control-Request-Method: POST" `
    -H "Access-Control-Request-Headers: content-type"
  ```

  Kỳ vọng: HTTP `200` hoặc `204`, có `Access-Control-Allow-Origin` đúng bằng origin staging và không có wildcard.

- [ ] Chạy lại preflight với một origin không được phép, ví dụ `https://not-allowed.invalid`.

  Kỳ vọng: không có header `Access-Control-Allow-Origin` khớp origin không được phép. Không coi riêng HTTP status là pass/fail; kiểm tra header trả về.

- [ ] Mở frontend staging bằng trình duyệt, xác nhận `/contact` tải được và Developer Tools không có lỗi CORS/network liên quan đến `/api/*`.

## C. Smoke API Contact bắt buộc

- [ ] Lấy access token Admin bằng luồng staging được phép. Gán vào **session PowerShell hiện tại**, không ghi token vào source, PR, history hay report:

  ```powershell
  $env:STAGING_ADMIN_ACCESS_TOKEN = '<token-tu-secure-session>'
  ```

- [ ] Nếu có account Customer hợp lệ, gán `STAGING_CUSTOMER_ACCESS_TOKEN` để kiểm tra nhánh `403`; nếu không có, ghi rõ check này chưa thực hiện thay vì giả định pass.
- [ ] Chạy smoke script chuẩn:

  ```powershell
  .\scripts\Test-ContactRequestStagingSmoke.ps1 `
    -ApiBaseUrl '<STAGING_API_BASE_URL>'
  ```

  Kỳ vọng: `STAGING_SMOKE_CONTACT=PASS`; report JSON có health `200`, public create `201`, duplicate `409`, validation `400`, anonymous list `401`, Admin list/filter/paging/detail/history `200`, missing resource `404`, invalid enum/note `400`, workflow `Pending → Contacted → Approved` và terminal reopen `409`.

- [ ] Nếu Customer token đã cấp, kiểm tra report có Customer list `403`.
- [ ] Chỉ khi được đội vận hành đồng ý và không ảnh hưởng tester khác, chạy probe rate-limit:

  ```powershell
  .\scripts\Test-ContactRequestStagingSmoke.ps1 `
    -ApiBaseUrl '<STAGING_API_BASE_URL>' `
    -IncludeRateLimitProbe `
    -RateLimitProbeCount <gia-tri-lon-hon-ContactPermitLimit>
  ```

  Kỳ vọng: ít nhất một HTTP `429`. Nếu không thấy, kiểm tra permit limit/window thực tế trước; không tự kết luận policy lỗi.

## D. Smoke frontend thủ công

- [ ] Mở `/contact`, gửi một request hợp lệ bằng email kỹ thuật duy nhất `@example.test` và lưu request ID/evidence. Kỳ vọng: UI báo thành công, không lộ error/token.
- [ ] Gửi validation sai có chủ đích (ví dụ subject quá ngắn) bằng môi trường được phép. Kỳ vọng: UI hiển thị lỗi có thể hiểu được, không crash.
- [ ] Đăng nhập Admin hoặc Editor bằng luồng hợp lệ, mở `/admin/contact-requests`. Kỳ vọng: list tải được, tìm kiếm/lọc trạng thái/phân trang/detail/history hoạt động.
- [ ] Thử cập nhật status đúng workflow trên một record kỹ thuật và xác nhận terminal status không thể reopen. Kỳ vọng: thành công với transition hợp lệ; transition cấm trả lỗi có thể hiểu được.
- [ ] Kiểm tra `/contact` và `/admin/contact-requests` ở viewport 360px. Kỳ vọng: không horizontal overflow, CTA/form/list/detail còn thao tác được.

## E. Evidence, cleanup và quyết định

- [ ] Lưu URL/SHA, `ps`, migrator log, health result, report JSON smoke và screenshot frontend theo chính sách evidence; xóa/che token, password, Authorization header và rendered compose có secret.
- [ ] Đánh dấu Contact kỹ thuật `example.test` đã tạo và chỉ xóa qua quy trình quản trị được review sau khi evidence được chấp nhận; không xóa trực tiếp bằng SQL không được review.
- [ ] Nếu bất kỳ bước bắt buộc nào fail, dừng cổng staging, lưu evidence không nhạy cảm, không retry migration mù và thực hiện fix/roll-forward qua branch + review.
- [ ] Chỉ đánh dấu staging pass khi tất cả bước bắt buộc đều có evidence; CI SHA tương ứng và cross-review thật cũng phải đạt.
