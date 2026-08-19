# Precheck tĩnh CI Contact

## Dữ kiện đã xác minh

Workflow CI chạy `dotnet restore`, sau đó `dotnet build CloudServiceStore.sln --configuration Release --no-restore`; backend test, frontend và Docker nằm sau build nên không chạy nếu build dừng. Workflow thiết lập .NET `10.0.x` và Node `24`.

Solution tham chiếu bốn project source và ba project test. Các file Contact hiện tại cần cho compile đều có mặt: Domain entity/enum, Application contracts/abstractions/service, EF configuration/repository, controller và named rate-limit extension. Không có file legacy `ContactEntities.cs` hay `ContactConfigurations.cs` trong source hiện tại để tạo trùng type với contract mới. Các project source/test đều target `net10.0`; project integration có `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory` và reference Web API.

## Giới hạn

Sandbox không có .NET SDK (`DOTNET_SDK_UNAVAILABLE`). Log chi tiết GitHub run #11/job `95669009727` không truy cập được vì endpoint trả 404. Vì thế precheck này **không xác định được nguyên nhân build fail**, không thay thế `dotnet build`, và không cho phép tuyên bố fix/pass.

## Việc cần có để tìm lỗi gốc

Trên clone `dev` hay GitHub runner có quyền, chạy:

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore -v:normal 2>&1 | tee build-release.log
```

Gửi `build-release.log`, đặc biệt từ mã lỗi đầu tiên `CSxxxx`/`NUxxxx` đến cuối block lỗi. Khi đó mới khoanh chính xác file/dòng và tạo patch tối thiểu.
