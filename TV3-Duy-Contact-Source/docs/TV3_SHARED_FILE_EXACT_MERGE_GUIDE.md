# Hướng dẫn merge chính xác — TV3 Contact Request

## Mục đích và cách đọc ảnh repository

Ảnh repository bạn cung cấp cho thấy đây là **repository GitHub thật**, có cấu trúc source đầy đủ (`src`, `frontend`, `tests`, `docs`, `.github`) và lịch sử commit. Vì vậy, archive TV3 chỉ là **nguồn file để đối chiếu**, không phải baseline để ghi đè. Mọi thao tác dưới đây phải bắt đầu từ branch `dev` mới nhất của repository đó; lấy đúng version source mà nhóm đang dùng để tránh làm mất commit gần đây của các thành viên khác.

> Các số dòng trong source archive chỉ là vị trí tham chiếu. Khi merge vào `dev`, hãy tìm **mốc neo bằng nội dung dòng code** được ghi dưới mỗi mục; không dán theo số dòng tuyệt đối, vì source `dev` có thể đã thay đổi.

## 0. Quy trình an toàn trước khi chép code

```bash
git fetch origin --prune
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management
```

Trước hết copy các file **không shared** của TV3 (entity/enum/configuration Contact, Application `ContactRequests`, repository, controller, extension rate-limit, page/component frontend, test và docs). Chỉ sau đó merge các đoạn trong tài liệu này vào file shared. Không dùng thao tác “Replace all” hay copy nguyên `Program.cs`, `DbContext.cs`, `DependencyInjection.cs`, `api.ts`, `appsettings.json` hoặc `docker-compose.yml`.

## 1. `CloudServiceStoreDbContext.cs`

**File đích:** `src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs`

| Mốc neo để tìm trong `dev` | Thao tác TV3 |
|---|---|
| `public DbSet<AffiliateApplicationStatusHistory> AffiliateApplicationStatusHistories => Set<AffiliateApplicationStatusHistory>();` | Chèn ngay sau dòng này, trước `AppUsers` nếu mốc đó còn tồn tại. |

Chỉ chèn hai dòng sau:

```csharp
public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
public DbSet<ContactRequestStatusHistory> ContactRequestStatusHistories => Set<ContactRequestStatusHistory>();
```

File hiện đã dùng `using CloudServiceStore.Domain.Entities;`; nếu `dev` chưa có namespace này thì thêm **một** dòng using đó, không thêm using trùng lặp.

**Không chèn** `modelBuilder.ApplyConfiguration(new ContactRequestConfiguration())` vào `OnModelCreating`. Baseline đang dùng:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(CloudServiceStoreDbContext).Assembly);
```

Do đó file độc lập `ContactRequestConfigurations.cs` sẽ tự được nạp. Không sửa query filter `SoftDeletableEntity`, audit timestamp, hay DbSet của Auth/Catalog/News/Order/Affiliate/Landing.

## 2. `DependencyInjection.cs`

**File đích:** `src/CloudServiceStore.Infrastructure/DependencyInjection.cs`

### 2.1. Using

Tìm nhóm using Application gần `CloudServiceStore.Application.Affiliates;` và chèn đúng một dòng:

```csharp
using CloudServiceStore.Application.ContactRequests;
```

### 2.2. Scoped services

Tìm cặp đăng ký Affiliate sau trong method `AddInfrastructure`:

```csharp
services.AddScoped<IAffiliateRepository, AffiliateRepository>();
services.AddScoped<IAffiliateService, AffiliateService>();
```

Chèn ngay sau cặp đó:

```csharp
services.AddScoped<IContactRequestRepository, ContactRequestRepository>();
services.AddScoped<IContactRequestService, ContactRequestService>();
```

Không di chuyển hoặc thay thế các đăng ký Auth, Catalog, Promotion, News, Landing, Order, Reporting hoặc Editor Workspace. `ContactRequestRepository` thuộc namespace `Infrastructure.Persistence`, nên không cần using mới nếu file vẫn có `using CloudServiceStore.Infrastructure.Persistence;`.

## 3. `Program.cs`

**File đích:** `src/CloudServiceStore.WebApi/Program.cs`

### 3.1. Namespace rate-limit

Nếu file `dev` chưa có, thêm cạnh các using Infrastructure:

```csharp
using CloudServiceStore.WebApi.Security;
```

### 3.2. Đăng ký rate limiter Contact — bắt buộc

Tìm đăng ký rate limit có sẵn:

```csharp
builder.Services.AddApiRateLimiting(builder.Configuration);
```

Chèn **ngay sau** nó:

```csharp
builder.Services.AddContactRequestRateLimiting(builder.Configuration);
```

Không thêm `app.UseRateLimiter()` lần hai. Pipeline nền phải chỉ có một middleware `app.UseRateLimiter();`, thường trước authentication/authorization. Extension TV3 chỉ thêm named policy `contact-requests`; nó không được có `OnRejected` cục bộ để không thay body 429 của Login, Order hoặc Affiliate.

### 3.3. Policy Admin/Editor — bắt buộc

Trong khối `builder.Services.AddAuthorization(options => { ... })`, tìm dòng policy gần nhất có `Admin`, `Editor`, ví dụ:

```csharp
options.AddPolicy("ManageAffiliates", policy => policy.RequireRole("Admin", "Editor"));
```

Chèn dòng sau trong cùng khối:

```csharp
options.AddPolicy("ManageContactRequests", policy => policy.RequireRole("Admin", "Editor"));
```

Không sửa policy `ManageUsers`, `ManageOrders`, `ManageNews`, JWT, CORS, thứ tự middleware, hay cơ chế bảo vệ refresh cookie.

### 3.4. Seed kỹ thuật local — tùy chọn, chỉ merge nếu nhóm muốn demo seed

Phần này **không cần** để Contact API hoạt động. Nếu nhóm bật demo technical seed, phải merge đồng thời cả ba đoạn dưới đây cùng thay đổi ở `DatabaseInitializer.cs`, `appsettings.json` và Docker Compose ở các phần sau.

Ngay sau dòng đọc `Seed:VisualQaData`, chèn:

```csharp
var tv3LocalDemoData = builder.Configuration.GetValue<bool>("Seed:Tv3LocalDemoData");
```

Ngay sau `VisualQaSeedGuard.Validate(...)`, chèn:

```csharp
if (tv3LocalDemoData && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Seed:Tv3LocalDemoData=true is only allowed in Development.");
```

Tại lời gọi `dbContext.SeedAsync(...)`, thêm `tv3LocalDemoData` sau argument `visualQaData`:

```csharp
await dbContext.SeedAsync(
    builder.Configuration["Seed:AdminPassword"],
    visualQaData,
    tv3LocalDemoData);
```

Không bật flag này trong shared/non-development environment. Seed chỉ tạo một Contact kỹ thuật `contact.demo@example.test` và một account Customer QA local nếu có admin password; không tạo testimonial, logo hoặc review giả.

## 4. `appsettings.json`

**File đích:** `src/CloudServiceStore.WebApi/appsettings.json`

Trong object `RateLimiting`, thêm key Contact cùng các permit limit hiện hữu. Nếu `OrderPermitLimit` là thuộc tính trước đó, dạng JSON đúng là:

```json
"OrderPermitLimit": 10,
"ContactPermitLimit": 5
```

Số `5` là default theo extension TV3; nhóm có thể đổi theo chính sách đã chốt, nhưng giữ số nguyên dương.

Nếu merge seed kỹ thuật tùy chọn, trong object `Seed` thêm:

```json
"VisualQaData": false,
"Tv3LocalDemoData": false
```

Không thay `ConnectionStrings`, JWT, CORS, NewsData hay Logging bằng dữ liệu trong archive. Không commit password thật; production/dev secret vẫn lấy từ secret store, `.env` local hoặc CI variables của nhóm.

## 5. `frontend/src/lib/api.ts`

**File đích:** `frontend/src/lib/api.ts`

### 5.1. Types Contact

Tìm `export type AffiliateStatus = 1 | 2 | 3 | 4;` trong vùng type declaration và chèn ngay sau:

```ts
export type ContactRequestStatus = 1 | 2 | 3 | 4 | 5;
export type ContactRequestConfirmation = { id: string; status: ContactRequestStatus; createdAt: string };
export type ContactRequestListItem = { id: string; fullName: string; email: string; phoneNumber: string; companyName?: string; subject: string; status: ContactRequestStatus; createdAt: string };
export type ContactRequestHistory = { id: string; fromStatus: ContactRequestStatus; toStatus: ContactRequestStatus; note?: string; changedBy?: string; createdAt: string };
export type ContactRequestDetail = ContactRequestListItem & { message: string; resolutionNote?: string; resolvedBy?: string; resolvedAt?: string; statusHistory: ContactRequestHistory[] };
```

### 5.2. Query helper và API module Contact

Tìm dấu kết thúc `reportPeriodQuery`, ngay trước `export const reportingApi = {`, chèn nguyên khối:

```ts
const contactRequestQuery = (params: { page?: number; pageSize?: number; search?: string; status?: ContactRequestStatus } = {}) => {
  const query = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 20) });
  if (params.search) query.set("search", params.search);
  if (params.status) query.set("status", String(params.status));
  return query.toString();
};

export const contactRequestsApi = {
  create: (body: { fullName: string; email: string; phoneNumber: string; companyName?: string; subject: string; message: string }) => apiFetch<ContactRequestConfirmation>("/api/v1/contact-requests", { method: "POST", body: JSON.stringify(body) }),
  all: (params: { page?: number; pageSize?: number; search?: string; status?: ContactRequestStatus } = {}, signal?: AbortSignal) => apiFetch<PagedResult<ContactRequestListItem>>(`/api/v1/contact-requests?${contactRequestQuery(params)}`, { signal }),
  detail: (id: string) => apiFetch<ContactRequestDetail>(`/api/v1/contact-requests/${id}`),
  updateStatus: (id: string, status: ContactRequestStatus, note?: string) => apiFetch<ContactRequestDetail>(`/api/v1/contact-requests/${id}/status`, { method: "POST", body: JSON.stringify({ status, note: note || null }) }),
};
```

`updateStatus` phải là **POST**, vì controller TV3 dùng `[HttpPost("{id:guid}/status")]`. Không “đồng bộ cho đẹp” thành PATCH theo Affiliate/Order, nếu không frontend sẽ nhận 405.

Không đụng vào `apiFetch`, refresh-token wrapper, authentication, News, Landing, Order, Affiliate, Reporting hoặc Dashboard client.

### 5.3. Hunk route guard/navigation Admin Contact — bắt buộc

**File đích:** `frontend/src/components/admin/admin-nav.ts`

`AdminSessionProvider` dùng `isRouteAllowed` từ chính file này để từ chối mọi route `/admin/*` chưa nằm trong `adminNavGroups`. Nếu không thêm hunk Contact, người dùng Admin/Editor sẽ bị redirect khỏi `/admin/contact-requests`, dù page/component Contact đã tồn tại. Đây là integration hẹp bắt buộc để route quản trị Contact hoạt động; không thay toàn bộ file navigation.

Trong import từ `@tabler/icons-react`, thêm `IconMessageCircle` cạnh nhóm icon hiện hữu. Trong group `Vận hành`, ngay sau item `/admin/orders`, chèn đúng item:

```ts
{
  title: "Yêu cầu liên hệ",
  href: "/admin/contact-requests",
  icon: IconMessageCircle,
  roles: ["Admin", "Editor"],
},
```

Hunk này đồng thời làm route được `isRouteAllowed` nhận diện và hiển thị menu cho Admin/Editor. Không thay roles của Orders/Affiliate, không sửa Dashboard/Workspace, và không thêm route của module khác.

## 6. `docker-compose.yml` và `.env.example`

**File đích:** `docker-compose.yml`

### 6.1. Hunk reliability cho SQL Server healthcheck — bắt buộc khi demo database mới

Trong `services.sqlserver.healthcheck.test`, healthcheck **chỉ được kiểm tra SQL Server đã nhận kết nối**, không được đòi database `CloudServiceStore` tồn tại trước khi migrator chạy. Tìm command cũ có `CloudServiceStore is not online` và thay duy nhất phần query bằng:

```yaml
-Q "SELECT 1" || exit 1
```

Lý do: `migrator` chờ `sqlserver: service_healthy`, nhưng `dotnet ef database update` mới là bước có thể tạo database trên volume mới. Nếu healthcheck đòi database có sẵn, lần chạy đầu sẽ tạo vòng chờ: SQL Server không healthy → migrator không chạy → database không được tạo.

Trong service `migrator`, sau `command`, có thể thêm rõ one-shot task:

```yaml
restart: "no"
```

Không đổi image SQL Server, ports, volume, connection string hay command `database update`. Đây là hunk reliability cho demo Docker dùng chung; review cùng owner file Docker nếu `dev` đã có healthcheck khác.

### 6.2. Seed kỹ thuật TV3 — tùy chọn

Trong `services.api.environment`, tìm:

```yaml
Seed__AdminPassword: ${SEED_ADMIN_PASSWORD}
```

Chèn ngay sau, **chỉ nếu merge seed tùy chọn**:

```yaml
Seed__Tv3LocalDemoData: ${SEED_TV3_LOCAL_DATA:-false}
```

Không đổi SQL Server image, command migrator, database name, ports, volume, JWT hay NewsData variables. Với healthcheck, chỉ áp dụng hunk `SELECT 1` ở mục 6.1; không thay nguyên khối healthcheck. Không đặt biến seed Contact ở service `migrator`, vì seed chỉ chạy trong API sau khi migration hoàn tất.

Nếu `.env.example` của nhóm có khai báo biến runtime cho API, có thể thêm dòng không bí mật sau (tùy chọn):

```dotenv
SEED_TV3_LOCAL_DATA=false
```

Không thêm `.env` thật vào Git.

## 7. Files shared bổ sung khi bật local technical seed

Nếu chọn bật seed TV3, cập nhật `DatabaseInitializer.cs` **theo hunk**, không thay nguyên file:

1. Đổi chữ ký `SeedAsync` để có parameter `bool tv3LocalDemoData = false` sau `visualQaData`.
2. Sau nhánh `VisualQaData`/`SeedDefaultAsync`, chèn:

```csharp
if (tv3LocalDemoData)
    await SeedTv3LocalDemoAsync(dbContext, adminPassword, cancellationToken);
```

3. Thêm private method `SeedTv3LocalDemoAsync` từ source TV3 trước `SeedIds`.

Đây là thay đổi **tùy chọn demo**, không phải điều kiện để Contact Request build hoặc chạy. Nếu không merge thì đồng thời bỏ phần 3.4 `Program.cs`, key seed ở appsettings và Compose.

## 8. Checklist review bắt buộc trước migration và PR

```bash
git diff --check
git diff -- src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  src/CloudServiceStore.Infrastructure/DependencyInjection.cs \
  src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/appsettings.json \
  frontend/src/lib/api.ts \
  frontend/src/components/admin/admin-nav.ts \
  docker-compose.yml
```

Diff của shared file chỉ được có block đã nêu trong tài liệu. Với `admin-nav.ts`, chỉ chấp nhận import `IconMessageCircle` và đúng item `/admin/contact-requests` cho Admin/Editor; không sửa item/module khác. Nếu xuất hiện thay đổi Auth, AppUser, JWT, refresh token, Catalog, Promotion, News, Landing, Order, Affiliate, Reporting, Dashboard, container image/volume/port, hãy loại khỏi branch TV3 hoặc trao đổi với owner file trước khi merge.

Sau khi build/test đạt trên clone thật mới tạo migration:

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure \
  --startup-project src/CloudServiceStore.WebApi \
  --output-dir Persistence/Migrations
```

Mở migration vừa sinh trước `database update`. Chỉ chấp nhận `CreateTable` cho `ContactRequests`, `ContactRequestStatusHistories`, index Contact và FK Contact. Nếu migration tạo/drop/alter `AppUsers`, `NewsArticles`, `Orders`, `Promotions`, `Affiliate*` hoặc bất cứ bảng ngoài Contact, xóa migration đó, quay lại `dev`/snapshot đúng rồi sinh lại; không chỉnh migration bằng tay để che sai lệch model.

## 9. Mệnh lệnh kiểm tra cuối cùng

```bash
dotnet build CloudServiceStore.sln --configuration Release
dotnet test CloudServiceStore.sln --configuration Release
cd frontend && npm run lint && npm run build
```

Chỉ sau khi ba nhóm lệnh này và Docker/CI chạy thật có log hoặc artifact, mới commit/push branch và mở PR. Hình repository chứng minh nhóm đã có repo/lịch sử thực để làm điều đó; archive không thay thế branch, commit author, review hay CI thật.
