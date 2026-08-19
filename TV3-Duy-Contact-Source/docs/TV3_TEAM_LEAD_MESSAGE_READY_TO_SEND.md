# Tin nhắn gửi team lead — copy dán

```text
Chào team lead,

Em xin tạo PR mới cho module Contact Request của TV3/Duy thay vì dùng lại PR #8.

Lý do: PR #8 đã closed/unmerged và CI fail ở `dotnet build CloudServiceStore.sln --configuration Release --no-restore`; sau lỗi build, backend test, frontend lint/build và Docker không chạy được nên PR cũ không có evidence CI đạt hay review thật.

Em đã tách lại phạm vi theo đúng phần Contact: Domain, Application service/validation/workflow, EF configuration/repository, Contact API, public/admin UI, tests, Postman, Docker override/scripts và docs. Các thay đổi ngoài phạm vi như AuthSecurityExtensions, Header/Nav, News, Landing, Order và Affiliate không đưa vào PR mới. Với file shared, em chỉ giữ hunk Contact cần thiết theo manifest, không ghi đè nguyên file.

Em đã chạy lại trên môi trường .NET 10:
- `dotnet restore` pass;
- Release build pass, 0 warning/0 error;
- 208/208 test pass (Domain 22, Application 117, Integration 69);
- frontend `npm ci`, lint và production build pass.

Em đã sửa ba lỗi thực tế: await sai ở SQL Server migration integration test, DataAnnotations record metadata cho ASP.NET Core .NET 10 và validation min-length của Contact service.

Phần còn lại trước merge: em sẽ sinh migration mới trực tiếp từ branch `dev` thật bằng `dotnet ef`, review để bảo đảm migration chỉ thay đổi hai bảng/index/FK Contact, sau đó chạy Docker database rỗng. Em cũng xin một reviewer kiểm tra architecture, authorization Admin/Editor, validation/workflow, rate limit, migration và shared-file diff.

Nhờ anh/chị xác nhận giúp em baseline `dev` và reviewer phù hợp để em mở PR mới theo đúng quy trình.
```
