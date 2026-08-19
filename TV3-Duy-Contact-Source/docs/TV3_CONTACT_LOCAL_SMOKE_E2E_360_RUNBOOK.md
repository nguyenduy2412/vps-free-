# Runbook smoke, E2E và responsive 360px — Contact Request TV3

## 1. Điều kiện local trước khi chạy

Chuẩn bị clone `dev` thật đã merge source/hunk Contact và migration Contact-only đã review. Docker daemon phải chạy trước smoke database rỗng; không dùng database có dữ liệu của nhóm.

| Thành phần | Kiểm tra |
|---|---|
| Backend | `.NET SDK 10`, restore/build/test pass. |
| Frontend | Node/npm, `npm ci` hoàn tất. |
| Docker | `docker info` pass; có `.env` local theo `.env.example`, không commit giá trị thật. |
| Database | Dùng override `docker-compose.contact-empty.yml`, volume riêng `contact-empty-sqlserver-data`. |

## 2. Smoke database rỗng + API health

Trên Windows PowerShell tại root clone:

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

Kỳ vọng: SQL `healthy`, migrator `exited (0)`, API `running` và health `200`. Khi migrator fail, dừng; lưu `ps`/migrator log không có secret, review migration checklist rồi sửa branch. Không chạy `down -v` trên volume ngoài Contact-empty.

## 3. Test API Contact local

Sau health, dùng Postman collection `tests/postman/CloudServiceStore_TV3.postman_collection.json` hoặc guide `TV3_CONTACT_API_QUICK_TESTS.md`. Kiểm tra tối thiểu các expected result:

| Luồng | Kỳ vọng |
|---|---|
| Public create hợp lệ | `201` và response có request ID. |
| Duplicate trong 24 giờ | `409 ProblemDetails`. |
| Payload validation sai | `400 ProblemDetails`. |
| List anonymous | `401`. |
| List Customer | `403`. |
| List/detail Admin hoặc Editor | `200`; paging/filter/history hoạt động. |
| Pending → Contacted → Approved | `200` theo status endpoint contract hiện hành. |
| Rejected/Cancelled không note | `400`. |
| Terminal reopen | `409`. |
| Public rate limit probe có kiểm soát | Có `429` khi vượt `ContactPermitLimit`; dùng email kỹ thuật unique. |

Không dùng token/password thật trong collection commit, shell history hay screenshot evidence.

## 4. Frontend lint, production build và E2E ba viewport

```bash
cd frontend
npm ci
npm run lint
NODE_ENV=production npm run build
env -u NODE_ENV npm run test:e2e:contact
```

PowerShell tương đương cho E2E khi shell đã đặt `NODE_ENV` không chuẩn:

```powershell
Remove-Item Env:NODE_ENV -ErrorAction SilentlyContinue
npm run test:e2e:contact
```

Kỳ vọng: Playwright chạy 27 test qua `desktop-chromium`, `mobile-chromium` (iPhone 13) và `mobile-360-chromium` (360×800). Test Contact phải bao gồm happy path, 400/409/429/network error, native validation, disabled double-submit và assertion không horizontal overflow tại 360px.

Trên runner ít RAM/CPU, thay lệnh E2E bằng `bash scripts/Run-ContactE2ELowResource.sh` hoặc thêm `--workers=1`. Xem `TV3_E2E_LOW_RESOURCE_PLAYWRIGHT_GUIDE.md`; không xem Chromium crash khi chạy song song là pass/fail chức năng trước khi retry một worker.

## 5. Kiểm tra thủ công giao diện 360px

Sau E2E, chạy frontend local (hoặc mở URL staging được phép) và dùng DevTools responsive `360 × 800`:

```bash
npm run dev -- --hostname 127.0.0.1 --port 3100
```

- [ ] Mở `/contact`: labels, input, textarea và nút submit không bị cắt hoặc tràn ngang.
- [ ] Submit success hiện request ID; lỗi validation/API/network hiển thị rõ và không làm vỡ layout.
- [ ] Nút submit disabled khi request pending; focus keyboard vẫn nhìn thấy.
- [ ] Mở `/admin/contact-requests` bằng Admin/Editor: search, filter, paging, detail/history và action status có thể thao tác trong viewport 360px.
- [ ] Không lộ token, password hay dữ liệu cá nhân trong screenshot/evidence.

Chỉ chụp screenshot/video sau khi chạy thật. Lưu command output, screenshot 360px và report Playwright theo quy trình evidence nhóm; không gắn nhãn “pass” nếu Docker/migration/CI/review thật chưa pass.
