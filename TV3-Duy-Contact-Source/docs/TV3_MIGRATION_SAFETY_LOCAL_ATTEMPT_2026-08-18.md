# Lần thử migration safety local — 18/08/2026

## Kết quả thực tế

Đã thử khởi động `scripts/Test-ContactRequestMigrationSafety.ps1` trong sandbox hiện tại. Lần chạy **không đạt điều kiện khởi động** vì snapshot source không phải Git clone và sandbox không có `pwsh`/Windows PowerShell.

| Điều kiện script yêu cầu | Kết quả sandbox | Ý nghĩa |
|---|---|---|
| Git work tree | `git rev-parse --is-inside-work-tree` trả exit `128`: `not a git repository` | Không thể so migration feature với `origin/dev`. |
| PowerShell runtime | Không có `pwsh` hoặc `powershell` khả dụng | Không thể parse/thực thi `.ps1` tại đây. |
| Migration Contact sinh từ dev | Chưa có (theo quy định không đóng gói migration thủ công) | Script đúng phải fail nếu chưa có exactly-one migration relative `origin/dev`. |

## Kết luận trung thực

Không có kết quả **pass** cho migration safety trong sandbox. Đây là blocker môi trường/evidence, không phải bằng chứng script lỗi. Script đã được kiểm tra tĩnh về các guard Contact-only, raw SQL, FK `AppUsers`, EF list và disposable SQL integration test.

## Lệnh phải chạy trên clone `dev` thật

```powershell
git fetch origin --prune
git switch feature/contact-request-management
git merge origin/dev

# Sau khi dotnet ef sinh migration Contact-only và review diff:
.\scripts\Test-ContactRequestMigrationSafety.ps1

# Chỉ khi có SQL Server disposable:
$env:CONTACT_TEST_SQLSERVER_CONNECTION_STRING = "Server=...;Database=ContactRequestIntegration_TV3;..."
.\scripts\Test-ContactRequestMigrationSafety.ps1 -RunSqlServerApply
```

Chỉ ghi migration safety pass khi report `artifacts/migration-safety-contact/MIGRATION_SAFETY_CONTACT_REPORT.md`, migration diff và (nếu dùng SQL Server) test log đều được tạo từ clone/branch thật.
