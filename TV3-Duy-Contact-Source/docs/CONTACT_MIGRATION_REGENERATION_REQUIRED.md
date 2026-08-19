# Contact Request — phải sinh lại migration trên branch `dev` thật

## Trạng thái an toàn hiện tại

Migration Contact tạo thủ công đã được **loại khỏi source bàn giao**. Không được commit migration khi `CloudServiceStoreDbContextModelSnapshot.cs` chưa chứa `ContactRequest` và `ContactRequestStatusHistory`.

> Không tự viết migration hoặc Designer bằng tay để thay thế cho EF Core. Migration phải được sinh từ source `dev` mới nhất bằng đúng .NET SDK và provider SQL Server của nhóm.

## Quy trình bắt buộc trên clone Git thật

```powershell
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management

dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release

dotnet ef migrations add AddContactRequestManagement `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi `
  --output-dir Persistence/Migrations
```

Sau lệnh trên, chỉ review các file migration/Designer/snapshot vừa sinh. Trong `Up()` chỉ được có hai bảng `ContactRequests`, `ContactRequestStatusHistories`, index Contact và FK Contact → `AppUsers` / history → Contact. `Down()` chỉ được drop hai bảng này theo thứ tự history rồi Contact.

## Kiểm tra trước commit

```powershell
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
dotnet ef migrations script `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi
dotnet ef database update `
  --project src/CloudServiceStore.Infrastructure `
  --startup-project src/CloudServiceStore.WebApi
```

Nếu diff migration có `CreateTable` hoặc `DropTable` cho `AppUsers`, `NewsArticles`, `Orders`, `Promotions`, `Affiliate*` hoặc bảng ngoài Contact, dừng ngay, xóa migration vừa sinh và đồng bộ lại branch `dev` trước khi sinh lại.
