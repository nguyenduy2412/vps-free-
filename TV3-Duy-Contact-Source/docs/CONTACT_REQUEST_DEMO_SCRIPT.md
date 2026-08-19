# Kịch bản demo Contact Request

## 1. Database rỗng

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
```

Chụp `docker compose ps` và `http://localhost:8080/health` làm minh chứng. Database dùng tên `CloudServiceStoreContactEmpty` và volume riêng.

## 2. Public form/API

Gọi `POST /api/v1/contact-requests` không có bearer token với body:

```json
{"fullName":"Nguyen Phuoc Duy","email":"duy@example.com","phoneNumber":"0901234567","companyName":"Cloud Demo","subject":"Tu van cloud","message":"Toi can tu van dich vu cloud cho doanh nghiep."}
```

Kỳ vọng `201 Created`, lưu request id và status `New`. Gọi lại cùng email trong 24 giờ để chứng minh `409` duplicate window.

## 3. Phân quyền quản trị

Đăng nhập Admin hoặc Editor, lấy access token theo flow Auth hiện có. Gọi `GET /api/v1/contact-requests?status=New&page=1&pageSize=20`, mở `GET /api/v1/contact-requests/{id}` và đối chiếu history ban đầu.

## 4. Đổi trạng thái

Gọi `PATCH /api/v1/contact-requests/{id}/status` lần lượt với `{"status":2,"note":"Da tiep nhan."}` và `{"status":3,"note":"Da goi tu van."}`. Chụp response detail, `ResolvedBy`, `ResolvedAt` và history. Dùng Customer gọi endpoint admin để chứng minh `403`; dùng status terminal chuyển ngược để chứng minh `409`.

## 5. Frontend

Mở `/contact` và `/admin/contact-requests` sau khi giao diện được merge. Quay video/ảnh các bước public submit, list, detail, status update và error state. Không dùng ảnh giả hoặc dữ liệu production.
