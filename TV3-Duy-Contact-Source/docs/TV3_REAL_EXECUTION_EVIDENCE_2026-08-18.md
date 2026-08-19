# Bằng chứng chạy thật — Contact Request

## Môi trường

Sandbox đã cài .NET SDK `10.0.110`. Source được restore/build trực tiếp từ `CloudServiceStore.sln` thay vì suy đoán từ CI log đã mất.

## Lỗi tìm được và đã sửa

| Lỗi | File | Sửa |
|---|---|---|
| `CS1061`: `IEnumerable<string>` không có `GetAwaiter` | `ContactRequestSqlServerMigrationTests.cs` | Bỏ `await` trước `GetSchema("Tables")`, vì ADO.NET API này đồng bộ. |
| ASP.NET Core .NET 10 ném lỗi record validation metadata property bị bỏ qua | `ContactRequestContracts.cs` | Chuyển DataAnnotations của record primary constructor từ `[property: ...]` sang `[param: ...]`. |
| Unit test không chặn message quá ngắn | `ContactRequestService.cs` | Đồng bộ validation nghiệp vụ với contract: tên 2–160, subject 3–180, message 10–4000. |

## Kết quả đã chạy

| Lệnh | Kết quả |
|---|---|
| `dotnet restore CloudServiceStore.sln` | Pass. |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` | Pass, 0 warning, 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` | Pass ở lần trước: Domain 22/22, Application 117/117, Integration 70/70; tổng 209 test. Coverage artifacts được tạo lại trong `TestResults/`. |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` sau test query Contact | Pass, 0 warning, 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build` sau test query Contact | Pass: Domain 22/22, Application 117/117, Integration 72/72; tổng **211/211** test. |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` sau controller hardening | Pass, 0 warning, 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` sau controller hardening | Pass: Domain 22/22, Application 117/117, Integration 75/75; tổng **214/214** test; 3 Cobertura XML được tạo lại. |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` sau Contact coverage test | Pass, 0 warning, 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` sau Contact coverage test | Pass: Domain 22/22, Application 121/121, Integration 77/77; tổng **220/220** test; 3 Cobertura XML được tạo lại. |
| `dotnet build CloudServiceStore.sln --configuration Release --no-restore` sau final pre-commit audit | Pass, 0 warning, 0 error. |
| `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` sau final pre-commit audit | Pass: Domain 22/22, Application 121/121, Integration 80/80; tổng **223/223** test; 3 Cobertura XML được tạo lại. |
| `npm ci` | Pass; npm báo 6 high-severity dependency vulnerabilities, chưa chạy `npm audit fix`. |
| `npm run lint` | Pass. |
| `env -u NODE_ENV npm run build` | Pass; `/contact` và `/admin/contact-requests` được build thành static routes. |
| `NODE_ENV=production npm run build` sau refactor metadata UI Contact | Pass: TypeScript, static generation 25 route, gồm `/contact` và `/admin/contact-requests`. |
| `npm run test:e2e:contact` | Pass: 27/27 Playwright tests (success/reset, 400/409/429 ProblemDetails, native required/email validation, network failure, loading/double-submit và overflow; Desktop, iPhone 13 Chromium, viewport 360px Chromium). API public được mock ở browser layer để E2E không phụ thuộc Docker/database. |

## Lưu ý frontend

Lần build đầu trong sandbox thất bại vì environment có `NODE_ENV=development`, Next.js báo non-standard value rồi prerender `/_global-error` lỗi. Chạy với `NODE_ENV` unset (điều kiện tương đương CI bình thường) đã build pass. Không sửa source chỉ để che lỗi environment sandbox.

## Chưa chạy

Docker không chạy trong sandbox vì không có Docker CLI/daemon. Migration database SQL Server thật cũng chưa chạy; migration Contact vẫn phải sinh lại từ branch `dev` thật trước PR. GitHub CI, PR/review thật vẫn phải xác minh lại trên repository có quyền truy cập.
