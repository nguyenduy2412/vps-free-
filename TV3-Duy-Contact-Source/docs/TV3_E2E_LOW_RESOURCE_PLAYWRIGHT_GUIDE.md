# Khắc phục crash Chromium khi E2E chạy trên runner ít tài nguyên

## Evidence đã quan sát

Lần chạy Contact E2E mặc định có ba worker đã đạt 26/27 trước khi một Chromium headless process crash tại case “submit button stays disabled while the Contact API request is pending”. Log chứa stack trace Chromium và không chỉ ra assertion Contact sai. Khi chạy lại cùng file test bằng **một worker**, toàn bộ **27/27** case đạt ở desktop, iPhone và viewport 360px. Đây là dấu hiệu cạnh tranh tài nguyên browser/renderer trên sandbox, không phải bằng chứng lỗi nghiệp vụ Contact.

| Chế độ | Kết quả trong workspace | Cách diễn giải |
|---|---|---|
| Mặc định `playwright test` (3 worker) | 26 pass, 1 Chromium crash | Không dùng làm evidence pass; giữ log để giải thích giới hạn runner. |
| `--workers=1` | 27/27 pass | Chế độ evidence ổn định cho runner ít RAM/CPU. |
| Evidence mock desktop + 360px, một worker | 4/4 pass | Chỉ chứng minh UI local/mock; không thay Docker/staging. |

## Lệnh khuyến nghị trên máy yếu

Từ root repository:

```bash
bash scripts/Run-ContactE2ELowResource.sh
```

Hoặc chạy trực tiếp:

```bash
cd frontend
env -u NODE_ENV npx playwright test e2e/contact-public.spec.ts --workers=1
```

Script cố định `--workers=1`, `--retries=0`, `--max-failures=1` và `--reporter=line`. Việc cố định này quan trọng hơn cho runner yếu so với khả năng chạy nhanh: chỉ có một Chromium worker sống tại một thời điểm, không nhân đôi test failed bằng retry (kể cả khi `CI=true`) và dừng sớm để tránh bão process/log. `--reporter=line` giảm khối lượng HTML report trong lần chẩn đoán local; trace/screenshot failure vẫn theo cấu hình Playwright.

`env -u NODE_ENV` loại biến môi trường shell không chuẩn trước khi Playwright khởi động `next dev`. Không đặt `NODE_ENV=production` cho lệnh E2E này; production mode dành cho `npm run build`. Script dùng `npx --no-install` để không tự tải package/browser lúc test; nếu thiếu Playwright/browser phải chạy `npm ci` và cài browser theo setup project trước.

## Checklist trước khi chạy

- [ ] Đóng browser/IDE preview nặng và các lần `next dev` hoặc Playwright còn sót.
- [ ] Kiểm tra RAM/swap bằng `free -h` trên Linux/WSL hoặc Task Manager trên Windows.
- [ ] Chạy `npm ci` một lần sau khi đổi lockfile; không chạy đồng thời với Playwright.
- [ ] Dùng script low-resource (cố định một worker) khi runner có RAM thấp, VM giới hạn hoặc đang chạy Docker/SQL Server cùng lúc.
- [ ] Không chạy `--headed`, không bật video/tracing cho toàn bộ test trên runner yếu. Cấu hình hiện tại chỉ giữ trace/screenshot khi failure.

## Khi vẫn crash

Chạy đúng project/case lỗi, một worker và giữ trace để phân biệt crash renderer với assertion ứng dụng:

```bash
cd frontend
env -u NODE_ENV npx playwright test e2e/contact-public.spec.ts \
  --project=desktop-chromium \
  --grep "submit button stays disabled" \
  --workers=1 \
  --trace=on
```

Nếu case pass tuần tự nhưng fail khi nhiều worker, ghi đây là **hạn chế tài nguyên runner**, dùng script low-resource cho evidence local và cấu hình CI đủ tài nguyên/worker phù hợp. Không set `PLAYWRIGHT_WORKERS` cho script vì nó sẽ fail fast để ngăn vô tình đưa lại parallelism. Nếu case vẫn fail tuần tự, mới xem là lỗi chức năng và kiểm tra `error-context.md`, network mock, console và source Contact trước khi tuyên bố pass.

## CI và evidence

Không lấy “27/27 local sequential” thay cho CI PR. Trong CI, giữ worker count phù hợp quota runner; nếu không biết quota, ưu tiên `--workers=1` để ổn định hơn tốc độ. Lưu command, worker count, SHA branch và output test cùng artifact CI.
