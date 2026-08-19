# Runbook thực thi Contact Request TV3 v50 từ clone `dev` thật

> **Mục tiêu:** áp dụng source Contact v45 vào baseline `dev` thật mà không ghi đè file shared, sinh migration Contact-only từ model `dev` đã merge và chỉ mở PR khi mọi evidence bắt buộc có thật. Không thay thế các lệnh dưới đây bằng kết quả từ ZIP/snapshot.

## 0. Điều kiện dừng trước khi bắt đầu

Chỉ chạy runbook khi thư mục làm việc là Git worktree của repository chính thức, `origin` trỏ remote nhóm được phép, branch `dev` fetch được và Docker daemon hoạt động nếu tới phần DB rỗng. Kiểm tra:

```bash
git rev-parse --is-inside-work-tree
git remote -v
git fetch --prune origin
git switch dev
git pull --ff-only origin dev
docker info
```

Nếu một lệnh fail, **dừng ở gate đó**. Không tự `git init`, không tự đoán remote, không dùng clone snapshot thay `dev`, không dùng Docker volume/database có dữ liệu nhóm để thử migration.

## 1. Tạo branch từ `dev` mới nhất

```bash
git status --short
git switch -c feature/contact-request-management
git rev-parse --short HEAD
```

`git status --short` phải rỗng trước khi copy. Ghi SHA `dev` làm baseline PR. Không làm việc trực tiếp trên `main`; không reuse branch/PR đã đóng.

## 2. Áp dụng source Contact v50 và shared hunk

Giải nén bundle v45 vào thư mục tạm, không giải đè repository. Chỉ copy source Contact TV3 được liệt kê trong manifest/runbook bundle. Những file shared sau đây phải merge từng hunk, **không copy thay thế toàn file**:

| Shared file | Hunk TV3 tối thiểu cần kiểm tra |
|---|---|
| `CloudServiceStoreDbContext.cs` | Hai `DbSet` Contact và model config assembly đang có. |
| `DependencyInjection.cs` | Đăng ký repository/service Contact. |
| `Program.cs` | Policy `ManageContactRequests`, CORS/rate-limit Contact theo source hiện hành. |
| `appsettings.json` | `RateLimiting:ContactPermitLimit`, `Cors:AllowedOrigins` rỗng baseline, seed false. |
| `frontend/src/lib/api.ts` | Contact types và `contactRequestsApi`, không xóa API module khác. |
| `frontend/src/components/admin/admin-nav.ts` | Chỉ import `IconMessageCircle` và item `/admin/contact-requests` cho Admin/Editor để route guard không redirect. |
| `docker-compose.yml` | Chỉ hunk migrator/healthcheck Contact đã được guide xác định, không rewrite service của thành viên khác. |

Thực hiện theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md` và `TV3_APPLY_SOURCE_TO_FEATURE_BRANCH.md`. Contract hiện hành dùng `POST /api/v1/contact-requests/{id}/status`; không đổi lại `PATCH`/workflow cũ chỉ vì template/guide lịch sử khác mô tả.

Sau copy/hunk, kiểm tra full diff theo baseline thay vì chỉ xem working tree:

```bash
git status --short
git diff --check
git diff dev...HEAD --stat
git diff dev...HEAD
git diff dev...HEAD -- src/CloudServiceStore.WebApi/Program.cs \
  src/CloudServiceStore.WebApi/Security/AuthSecurityExtensions.cs \
  src/CloudServiceStore.Infrastructure/Persistence/CloudServiceStoreDbContext.cs \
  frontend/src/lib/api.ts \
  frontend/src/components/admin/admin-nav.ts
```

Nếu thấy News, Landing, Customer, Affiliate, Order, Auth, Header/Nav hoặc nguyên file shared bị thay thế ngoài hunk TV3, dừng và bỏ phần ngoài phạm vi trước khi tiếp tục.

## 3. Build/test tối thiểu trước migration

```bash
dotnet restore CloudServiceStore.sln
dotnet build CloudServiceStore.sln --configuration Release --no-restore
dotnet test CloudServiceStore.sln --configuration Release --no-build --no-restore

cd frontend
npm ci
npm run lint
NODE_ENV=production npm run build
cd ..
bash scripts/Check-ContactControllerArchitecture.sh
bash scripts/Run-ContactE2ELowResource.sh
```

`NODE_ENV=production` được dùng cho production build để tránh môi trường shell kế thừa giá trị không chuẩn. Script E2E low-resource cố định một worker/zero retry để tránh crash Chromium trên runner yếu; không dùng kết quả snapshot/ZIP thay cho evidence branch `dev` này. Không tự chạy `npm audit fix` trong branch TV3 chỉ vì audit warning; đánh giá dependency update phải theo phạm vi/review riêng.

## 4. Sinh và review migration Contact-only

Chỉ khi phần 3 đều pass, chạy trên **branch TV3 từ `dev` thật**:

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure/CloudServiceStore.Infrastructure.csproj \
  --startup-project src/CloudServiceStore.WebApi/CloudServiceStore.WebApi.csproj \
  --output-dir Persistence/Migrations

git status --short
git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

Review cả `Up()` và `Down()`. Migration được chấp nhận chỉ chứa `ContactRequests`, `ContactRequestStatusHistories`, index và FK thật sự liên quan Contact. Nếu xuất hiện `CreateTable`/`DropTable` cho `AppUsers`, News, Orders, Promotions, Affiliate hoặc module khác, **không sửa tay để che drift**: xóa migration vừa sinh, đồng bộ/review baseline `dev` và tìm nguyên nhân model drift trước khi sinh lại.

Sau migration đã được review, chạy lại build/test/frontend validation ở phần 3 trên chính branch có migration. Không dùng số 225/225 historical trong ZIP để tick pass PR; chỉ lưu output command mới cùng SHA branch.

## 5. Docker database rỗng và API health

Trên máy có Docker daemon, dùng volume/database riêng Contact-empty:

```powershell
./scripts/Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

Kỳ vọng: SQL Server healthy, migrator `exited (0)`, API running và `/health` trả `200`. Chỉ `-Reset` volume Contact-empty; không chạy `down -v` trên database nhóm/staging. Khi fail, lưu `ps`/log không có secret rồi sửa nguyên nhân trên branch.

## 6. Smoke/E2E/responsive và audit scope

Sau Docker local, chạy smoke API public create/duplicate/validation, Admin/Editor list-detail-status, Customer `403`, anonymous `401` và rate-limit `429` theo `TV3_STAGING_POST_DEPLOY_SMOKE_CHECKLIST.md`. Chạy E2E/360px theo phần 3 và chỉ gắn screenshot UI mock là local/mock. Trước commit:

```bash
git diff --check
git diff --name-only origin/dev...HEAD
git status --short
```

Đối chiếu từng file với phạm vi TV3. Không cho artifact build, `node_modules`, `.next`, `.env`, token, log chứa secret hay file source module khác vào staging area.

## 7. Identity, commit, push và PR

Identity phải là account/email GitHub TV3 thật, không bịa/đổi author:

```bash
git config user.name
git config user.email
git add <danh-sach-file-da-review>
git commit -m "feat(contact): add contact request management"
git push -u origin feature/contact-request-management
```

Sau push, mở PR từ `feature/contact-request-management` vào `dev`, dán `TV3_PR_BODY_READY_TO_COPY.md`, đính evidence thật và request TV2/collaborator review. CI PR mới phải có restore, Release build, backend test + coverage, `npm ci`, lint, frontend production build và Docker build. Không tự approve/review giả. Chỉ gửi link PR cho nhóm trưởng khi CI của **commit cuối** xanh và review collaborator đã submit. Chỉ merge khi migration, Docker DB rỗng, build/test/lint/build/E2E, scope diff, CI và review đều đạt.
