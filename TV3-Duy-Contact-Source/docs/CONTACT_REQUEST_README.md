# Contact Request Management — Hướng dẫn chạy

## Phạm vi

Module TV3 quản lý form liên hệ công khai và workflow xử lý của Admin/Editor. Provider chính thức là SQL Server; PostgreSQL không nằm trong scope migration hiện tại.

## Yêu cầu

Docker Desktop, .NET SDK 10, Node.js 24 và PowerShell 7 hoặc Windows PowerShell.

## Chạy local từ database rỗng

```powershell
Copy-Item .env.example .env
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
```

Tạo migration sau khi build/test đạt:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Build-And-AddContactRequestMigration.ps1
```

Review migration Contact-only, sau đó chạy `Test-ContactRequestMigrationSafety.ps1`. Script Docker chỉ được chạy khi migration đã sinh từ clone `dev` thật. Xem đầy đủ tại `TV3_DOCKER_DEMO_QUICK_START.md`; staging dùng `TV3_STAGING_DEPLOYMENT_GUIDE.md`.

## Test

```powershell
dotnet test CloudServiceStore.sln --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults
```

Test migration thật chỉ chạy khi đặt `CONTACT_TEST_SQLSERVER_CONNECTION_STRING` tới database disposable có tên `ContactRequestIntegration_*`.

## Demo

Gửi request tại `/contact`; đăng nhập Admin/Editor, mở `/admin/contact-requests`, lọc/tìm, xem history và đổi `Pending → Contacted → Approved` hoặc `Rejected/Cancelled` với note. Terminal status không được reopen. Dùng Customer thử API admin để minh chứng `403`.

## GitHub

Tạo branch `feature/contact-request-management`, mở PR riêng, yêu cầu review chéo và lưu link review. Mục tiêu 10 PR là bằng chứng GitHub của cả nhóm, không thể thay bằng code local.
