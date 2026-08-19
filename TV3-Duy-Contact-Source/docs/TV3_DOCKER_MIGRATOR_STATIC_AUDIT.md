# Audit Docker Migrator — Contact Request TV3

**Thời điểm rà soát:** 18/08/2026
**Phạm vi:** `docker-compose.yml`, `docker-compose.contact-empty.yml`, `deploy/api.Dockerfile`, `scripts/Run-ContactRequestEmptyDatabase.ps1`, source khởi tạo database và toàn bộ evidence log đang có trong bundle TV3.

## 1. Kết luận về log hiện có

Không có file log thực tế từ lệnh `docker compose logs migrator` trong source repository hoặc bundle TV3. Môi trường rà soát cũng không có Docker CLI/daemon, vì vậy không thể chạy lại container hoặc tuyên bố migrator đã thành công.

Các file `.log` hiện có chỉ là evidence cho `dotnet restore/build/test`, frontend lint/build và Playwright. Các tài liệu có cụm từ `migrator`, kể cả `docs/diagnose-migrator-143-todo.md`, là **checklist/chỉ dẫn thao tác**, không phải output Docker. Tên file này không đủ để kết luận đã có một lần chạy thật exit `143`.

> Trạng thái chứng minh hiện tại: **Docker migrator — NOT RUN / không có log thật để phân tích.**

## 2. Phát hiện từ kiểm tra tĩnh

| Mức độ | Phát hiện | Bằng chứng cấu hình | Tác động có thể xảy ra | Hành động bắt buộc |
|---|---|---|---|---|
| **Đã sửa, cần chạy thật** | Compose chính từng có nguy cơ deadlock ở lần chạy đầu với volume rỗng. | Healthcheck hiện chỉ kiểm tra SQL Server sẵn sàng bằng `sqlcmd ... -Q "SELECT 1"`; migrator vẫn chờ `service_healthy`. | Bỏ vòng phụ thuộc database phải tồn tại trước khi migration tạo database. | Chạy chứng minh database rỗng thật. |
| **Trung bình** | Override `contact-empty` cũng kiểm tra `SELECT 1`, nhưng chỉ được xác minh tĩnh. | Override đổi connection string API/migrator sang `CloudServiceStoreContactEmpty` và dùng volume riêng. | Luồng DB rỗng có thiết kế đúng hướng nhưng vẫn có thể lỗi runtime do image, EF tool, migration hoặc secret. | Chạy compose override và lưu log/exit code thật. |
| **Đã sửa, cần chạy thật** | Script từng đọc exit code migrator ngay sau `up --detach`. | Script hiện poll tối đa 180 giây, chỉ đọc `ExitCode` sau state `exited`, in log SQL/migrator khi timeout hoặc state bất thường. | Giảm false failure khi image build hoặc migration đang chạy. | Chạy script trên Docker Desktop và lưu output. |
| **Trung bình** | Không có healthcheck hoặc timeout riêng cho migrator. | Service migrator chỉ có `depends_on: sqlserver: service_healthy`; command chạy `dotnet-ef database update`. | Lỗi migration có thể chỉ thấy qua exit code/log; migrator treo lâu sẽ làm API không khởi động nhưng không có diagnostic tập trung. | Khi chạy thật, áp timeout vận hành và lưu `ps -a`, `logs migrator`, `logs sqlserver`. Có thể bổ sung `restart: "no"` để thể hiện rõ one-shot task nếu convention nhóm cho phép. |
| **Thấp** | API vẫn có seed/retry database startup sau migrator. | `DatabaseInitializer` chỉ seed, không gọi `Migrate`; `DatabaseStartup` retry tối đa 5 lần với backoff 2–10 giây. | Có thể có warning retry nếu database/seed chưa sẵn sàng; đây không phải migration chạy hai lần. | Phân biệt warning seed/retry của API với lỗi migrator khi đọc log. |

## 3. Điểm đã đúng về thiết kế

Migrator dùng target riêng `migrator` trong `deploy/api.Dockerfile`, khôi phục local tool rồi chạy `dotnet tool run dotnet-ef`. Lệnh Compose truyền `database update` với đúng Infrastructure project và WebApi startup project. API chờ điều kiện `service_completed_successfully`, nên về nguyên tắc API không được khởi động khi migrator báo lỗi.

Override `docker-compose.contact-empty.yml` dùng database `CloudServiceStoreContactEmpty` và volume `contact-empty-sqlserver-data` riêng. Đây là cách ly đúng; không dùng lại volume mặc định `sqlserver-data` khi kiểm tra DB rỗng.

## 4. Lệnh thu thập log bắt buộc trên máy có Docker

Từ clone `dev` thật, sau khi tạo migration Contact-only bằng `dotnet ef`, chạy các lệnh sau. Không che hay rút gọn các dòng lỗi khi lưu evidence.

```powershell
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml config | Out-File docker-compose.resolved.log
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml up --build --detach
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a | Tee-Object docker-compose.ps.log
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator | Tee-Object docker-migrator.log
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color sqlserver | Tee-Object docker-sqlserver.log
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color api | Tee-Object docker-api.log
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps --all migrator --format '{{.Status}} | exit={{.ExitCode}}'
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

Mức pass tối thiểu là SQL Server `healthy`, migrator có trạng thái `exited` với `exit=0`, API đang chạy và `/health` trả `200`. Nếu migrator thất bại, phải lưu cùng lúc `docker-migrator.log`, `docker-sqlserver.log`, `docker-compose.resolved.log` và output `ps -a`; không được chỉ gửi một ảnh màn hình.

## 5. Dấu hiệu cần tìm trong log thực tế

| Dấu hiệu trong log | Diễn giải khả dĩ | Việc làm |
|---|---|---|
| `Login failed for user 'sa'` hoặc `Cannot open database` | Secret/connection string/volume cũ không đồng nhất. | Kiểm tra `.env`, resolved Compose config và chỉ reset volume `contact-empty-sqlserver-data` khi đang test DB rỗng. |
| `No executable found matching command "dotnet-ef"` | `dotnet-tools.json` không được copy/restore đúng hoặc Dockerfile migrator build lỗi. | Kiểm tra `dotnet tool restore` và image build log. |
| `PendingModelChangesWarning`, migration ngoài Contact, hoặc migration tạo bảng của module khác | Model drift/migration tạo từ baseline sai; đây là blocker. | Dừng, không áp vào dev; sinh lại migration từ clone `dev` thật và review `Up`/`Down`. |
| Migrator `exit 143` | Container bị dừng bằng tín hiệu (thường do stop/recreate/timeout), không phải pass. | Lưu `ps -a`, log SQL/API, xác nhận có thao tác stop/rebuild và chạy lại từ volume rỗng. |
| SQL Server `unhealthy` trước migrator | Có khả năng gặp deadlock healthcheck Compose chính đã nêu ở mục 2. | Dùng override DB rỗng hoặc sửa healthcheck chính trước khi chứng minh lần chạy đầu. |

## 6. Trạng thái sau audit

Đã sửa tĩnh hai rủi ro chính: healthcheck Compose chính chỉ còn kiểm tra SQL Server sẵn sàng và script chờ migrator hoàn tất trước khi đánh giá exit code. Chưa có log Docker thật nên không có cơ sở nói bản sửa đã chạy thành công trong container. Docker/migration database rỗng vẫn phải được xác minh trên máy có Docker sau khi migration Contact-only được sinh từ clone `dev` thật.
