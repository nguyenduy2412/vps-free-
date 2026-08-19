# Trạng thái blocker PR #8 — Contact Request

## Nguồn trạng thái

Tài liệu này ghi nhận theo thông tin kiểm tra trực tiếp PR #8 do nhóm cung cấp. Nó thay thế mọi claim archive trước đó về build/test/CI/PR nếu các claim đó không có log hoặc artifact GitHub tương ứng.

## Trạng thái hiện tại

| Hạng mục | Trạng thái | Ý nghĩa |
|---|---|---|
| PR #8 | Closed, `merged: false` | Không còn là PR đang chờ merge/review. |
| GitHub Actions run #11 | Failure | Không có evidence CI xanh. |
| Backend Release build | Fail tại `dotnet build CloudServiceStore.sln --configuration Release --no-restore` | Đây là blocker đầu tiên cần sửa. |
| Log lỗi chi tiết run #11 / job `95669009727` | Không truy cập được; endpoint trả `404/Not Found` | Chưa xác định mã `CSxxxx`/`NUxxxx`, file hay dòng lỗi; không suy đoán nguyên nhân. |
| Backend test | Chưa chạy | Job dừng sau build fail. |
| Frontend `npm ci`, lint, production build | Chưa chạy | Job dừng trước frontend. |
| Docker build | Chưa chạy | Chưa có evidence database/image/migrator chạy. |
| Review | 0 review, 0 review thread, 0 comment | Chưa có review chéo thật. |
| Reviewer được request | Có request tới `0023411000-sketch` nhưng chưa submit | Không được tính là review. |

## Phạm vi source cần giữ và kiểm tra

Code Contact cơ bản có các lớp Domain, Application, Infrastructure, Web API, frontend và file test. Đây chỉ là **source tồn tại**, không phải bằng chứng solution build hoặc test pass.

PR #8 từng thay đổi 20 file, gồm các file shared như `admin-nav.ts`, `site-header.tsx`, `DependencyInjection.cs`, DbContext, `Program.cs` và `AuthSecurityExtensions.cs`. Trước PR thay thế, chỉ được giữ hunk Contact cần thiết; đặc biệt không được lấy lại `AuthSecurityExtensions.cs` từ overlay corrected và không đưa diff Navigation/Header ngoài phạm vi Contact.

## Điều kiện để PR thay thế đạt DoD

1. Lấy log lỗi build đầu tiên từ CI hoặc chạy trên clone `dev` thật, sửa rồi chạy lại Release build.
2. Chạy thành công backend test, frontend `npm ci`, lint, build, Docker database rỗng và migration Contact mới sinh từ `dev`.
3. Đính kèm log/artifact thật, cập nhật checkbox PR thành hoàn tất sau khi từng bước pass.
4. Mở PR mới, request reviewer và nhận review/comment thật về architecture, authorization, validation, test, regression và shared-file diff.
5. Chỉ merge sau khi CI xanh, review hợp lệ và migration chỉ thay đổi Contact.
