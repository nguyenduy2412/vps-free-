# Trạng thái bằng chứng TV3

## Đã có bằng chứng trong sandbox

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| Frontend lint | Pass ở lần kiểm tra sau cập nhật News/Contact | `npm run lint` đã chạy không trả lỗi. |
| TypeScript | Pass ở lần kiểm tra sau cập nhật News/Contact | `npx tsc --noEmit` đã chạy không trả lỗi. |
| JSON Postman | Pass | Collection và environment được parse bằng Node; 35 request. |
| ZIP source | Pass integrity | `unzip -t` cho các archive bàn giao không báo lỗi. |
| Migration thủ công Contact | Đã loại | Snapshot không có Contact; không dùng migration thủ công làm evidence. |

## Chưa có bằng chứng và không được ghi là pass

| Hạng mục | Trạng thái thật | Cách xác minh bắt buộc |
|---|---|---|
| `dotnet build` | Chưa chạy: sandbox không có .NET SDK (`dotnet: command not found`) | Chạy `dotnet build CloudServiceStore.sln --configuration Release` trên clone Git thật. |
| `dotnet test` / coverage | Chưa chạy | Chạy test Release và lưu log/coverage artifact. |
| EF migration Contact | Blocked cho đến khi sinh từ `dev` thật và snapshot đúng | Làm theo `CONTACT_MIGRATION_REGENERATION_REQUIRED.md`. |
| Docker/SQL Server | Chưa chạy: sandbox không có Docker daemon | Chạy compose database rỗng, kiểm tra migrator exit `0`, `/health` 200 và Postman 429. |
| Git branch/commit/PR/CI/review | Chưa có: snapshot không có `.git`, remote hay identity | Thực hiện trên clone GitHub của nhóm với account/CI/collaborator thật. |

## Quy tắc bàn giao

Không dùng số test, coverage, PR, review hoặc CI trong archive làm evidence cuối cùng nếu không có log/link/artifact sinh từ repository Git thật của nhóm.
