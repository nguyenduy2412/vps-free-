# Contact Request Management — TV3/Duy

Module này cung cấp form liên hệ công khai tại `/contact` và luồng quản trị dành cho `Admin`/`Editor` tại `/admin/contact-requests`. Phạm vi chỉ gồm Contact Request: Domain, Application, Infrastructure, Web API, frontend Contact, test, script kiểm tra và hướng dẫn merge hunk shared tối thiểu.

## Quy tắc tích hợp

Gói này phải được áp dụng lên một branch tạo từ `dev` thật của CloudServiceStore. Không thay thế toàn bộ các file dùng chung. Chỉ merge các hunk Contact vào `DbContext`, DI, `Program.cs`, `appsettings.json`, `frontend/src/lib/api.ts`, `admin-nav.ts` và Compose theo [hướng dẫn shared](TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md).

Migration chính thức không nằm trong gói. Sau khi merge source và build/test thành công trên baseline thật, sinh migration EF Core bằng `dotnet ef migrations add AddContactRequestManagement` rồi xác minh migration chỉ chứa bảng, index và FK của Contact. Quy trình chi tiết ở [hướng dẫn migration](TV3_CONTACT_ONLY_MIGRATION_DEV_EXECUTION.md).

## Chạy và kiểm thử

Yêu cầu: .NET SDK 10, Node.js, SQL Server qua Docker Desktop và PowerShell. Tại clone CloudServiceStore thật, chạy `dotnet build CloudServiceStore.sln --configuration Release`, `dotnet test CloudServiceStore.sln --configuration Release`, sau đó vào `frontend` chạy `npm ci`, `npm run lint` và `npm run build`.

Để kiểm tra database rỗng sau khi đã có migration Contact-only, dùng `scripts/Run-ContactRequestEmptyDatabase.ps1 -Reset`. Test SQL Server chỉ được dùng database disposable có tên bắt đầu `ContactRequestIntegration_` qua biến `CONTACT_TEST_SQLSERVER_CONNECTION_STRING`.

## Giới hạn và vận hành

Public submit bị giới hạn theo IP. Khi triển khai sau reverse proxy, baseline phải cấu hình `UseForwardedHeaders` với trusted proxy/network **trước** `UseRateLimiter` để `RemoteIpAddress` là client IP đã được forwarding; không tự tin tưởng header từ Internet. Tìm kiếm dạng `Contains` là giới hạn MVP và có thể scan khi dữ liệu lớn; chỉ thêm full-text search sau khi có yêu cầu và phê duyệt riêng.

Không có PDF, DOCX, báo cáo tiến độ, evidence lịch sử hoặc slide trong feature PR này. Những hồ sơ nộp đó được quản lý ngoài PR code để review feature tập trung và minh bạch.
