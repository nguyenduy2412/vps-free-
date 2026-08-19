# Audit nội dung Docker, test và migration được gửi bổ sung

## Kết luận

Không áp dụng nguyên văn snippet được gửi. Snippet đó dựa trên một project/contract cũ, trong khi source hiện tại dùng `CloudServiceStore.WebApi`, `CloudServiceStoreDbContext`, DTO `UpdateContactRequestStatusRequest`, workflow `Pending/Contacted/Approved/Rejected/Cancelled` và migrator container riêng. Chèn nguyên văn sẽ gây lỗi build, sai API contract hoặc đưa password mẫu vào Git.

## 1. Unit test UpdateStatus

| Nội dung snippet | Trạng thái | Lý do |
|---|---|---|
| `UpdateContactRequestStatusCommand` | Không tương thích | Source hiện tại dùng `UpdateContactRequestStatusRequest`. |
| `ContactRequestStatus.InProgress/Resolved` | Không tương thích | Enum hiện tại là `Pending`, `Contacted`, `Approved`, `Rejected`, `Cancelled`. |
| Service trả `bool` | Không tương thích | `UpdateStatusAsync` trả `ContactRequestDetailDto` hoặc ném exception nghiệp vụ. |
| Controller `NotFoundResult` | Không tương thích | Controller hiện map `ContactRequestNotFoundException` thành `ProblemDetails` 404. |

`ContactRequestsControllerTests.cs` hiện đã có test `UpdateStatus_passes_claim_actor_and_ip_to_service` và `UpdateStatus_returns_400_problem_details_for_validation_error`. Các test này đúng signature hiện tại, đã được chạy trong bộ Integration Tests 69/69 pass. Không thay bằng snippet cũ.

## 2. Backend Dockerfile

Snippet dùng `src/CloudServiceStore.Api/CloudServiceStore.Api.csproj`, `.Api.dll` và image preview. Các đường dẫn này không tồn tại trong source hiện tại. Dockerfile đúng đang ở `deploy/api.Dockerfile`, dùng:

```text
src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj
CloudServiceStore.WebApi.dll
mcr.microsoft.com/dotnet/sdk:10.0
mcr.microsoft.com/dotnet/aspnet:10.0
```

Dockerfile hiện có target `migrator` chạy local `dotnet-ef` tool, phù hợp chiến lược migration của project. Không thêm Dockerfile backend thứ hai cho module Contact.

## 3. Frontend Dockerfile

Snippet frontend copy `.next/standalone` và chạy `server.js`, nhưng `frontend/next.config.ts` hiện không đặt `output: "standalone"`. Vì vậy snippet này sẽ fail ở bước copy nếu không thay đổi cấu hình deployment toàn dự án. TV3 Contact không tự thay đổi chiến lược frontend deployment chung.

Nếu nhóm cần production frontend container, TV1/team lead phải chốt cách deploy và sửa `next.config.ts`/Compose trong một PR foundation riêng. Không đưa thay đổi đó vào PR Contact.

## 4. Docker Compose

Snippet compose có các lỗi/rủi ro sau:

| Snippet cũ | Source hiện tại |
|---|---|
| Hard-code `YourStrong@Password123` | Dùng `${MSSQL_SA_PASSWORD}` từ môi trường; không commit password. |
| `ConnectionStrings__DefaultConnection` | Dùng `ConnectionStrings__CloudServiceStore`. |
| Service `db` / `CloudServiceStore.Api` | Dùng `sqlserver` / `CloudServiceStore.WebApi`. |
| API chờ database đơn thuần | API chờ `migrator` hoàn tất thành công. |
| Không healthcheck | SQL Server có healthcheck trước migrator. |

Docker Compose đúng đã tồn tại ở root: `docker-compose.yml`, gồm `sqlserver`, `migrator`, `api`. TV3 chỉ có `docker-compose.contact-empty.yml` để kiểm tra database rỗng riêng.

## 5. Auto migration trong Program.cs

Không thêm block `ApplicationDbContext`/`Database.MigrateAsync()` từ snippet. `ApplicationDbContext` không tồn tại; Program hiện tại đã dùng `CloudServiceStoreDbContext`, `DatabaseStartup.ExecuteWithRetryAsync` và `SeedAsync`. Migration schema được áp dụng rõ ràng bởi service `migrator` trong Compose, giúp API chỉ khởi động khi migration thành công. Đó là cách an toàn hơn việc mọi API container tự đua chạy migration khi scale.

## 6. Lệnh đúng để chạy trực tiếp

Tại root chứa `CloudServiceStore.sln`:

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults

cd frontend
npm ci
npm run lint
env -u NODE_ENV npm run build
```

## 7. Lệnh đúng để chạy Docker sau khi migration Contact được sinh từ `dev`

```bash
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml up --build --detach
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs migrator
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs api
```

Chỉ khi migrator exit `0` và API health `200` mới kiểm tra Contact API/UI. Khi dừng, không xóa volume dùng chung; chỉ reset volume database rỗng nếu nhóm đã xác nhận không cần dữ liệu đó.
