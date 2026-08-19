# Quy trình Git/GitHub thật — Contact Request TV3/Duy

Tài liệu này áp dụng cho phần `feature/contact-request-management` của TV3/Duy. Archive không có `.git`, remote hoặc GitHub identity nên **không phải evidence** cho branch, commit, PR, CI hay review. Các bước dưới đây phải được TV3 thực hiện từ clone chính thức bằng account/email GitHub của chính mình.

## Quy tắc không được vi phạm

| Quy tắc | Thực hiện đúng |
|---|---|
| `main` là release | Không push trực tiếp vào `main`; chỉ nhóm trưởng quyết định merge từ `dev` sang `main` sau khi các feature được nghiệm thu. |
| Feature branch | TV3 tạo branch mới từ `dev` mới nhất; không sửa branch của TV2 hoặc branch cũ đã closed. |
| Commit identity | Dùng tên/email GitHub thật của TV3; không sửa author, không tạo commit giả và không nhờ người khác đứng tên. |
| PR | Push branch TV3, mở PR riêng vào `dev`, có mô tả scope/test/evidence rõ ràng. |
| Review/CI | Chỉ merge khi GitHub Actions xanh và collaborator review thực chất/approve. Copilot review đơn lẻ không thay review collaborator. |
| Số PR nhóm | Mục tiêu tối thiểu 10 PR hợp lệ là kiểm tra ở GitHub thật; không tự tính PR upload nhầm/trùng như #1, #3, #6. |

## Lệnh TV3 thực hiện trên clone chính thức

```bash
git fetch origin --prune
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management

git config user.name "<tên GitHub thật của TV3>"
git config user.email "<email GitHub thật của TV3>"
git config --get user.name
git config --get user.email
```

Copy 24 file source/UI/test/script Contact và merge từng hunk shared theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`; không chép đè nguyên `Program.cs`, DbContext, DI, API client, appsettings hoặc docker-compose. Sau khi sinh migration Contact-only trên clone `dev`, kiểm tra:

```bash
git diff --check
dotnet build CloudServiceStore.sln --configuration Release
dotnet test CloudServiceStore.sln --configuration Release
cd frontend && npm ci && npm run lint && NODE_ENV=production npm run build
```

Chỉ stage file đúng scope, rồi commit và push với account TV3:

```bash
git add src/CloudServiceStore.Domain src/CloudServiceStore.Application/ContactRequests \
  src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs \
  src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs \
  src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs \
  src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs \
  frontend/src tests scripts docs
git commit -m "feat(contact): add request query filtering and paging tests"
git push -u origin feature/contact-request-management
```

Sau đó TV3 mở PR **`feature/contact-request-management` → `dev`**, dán mô tả tại `TV3_PR_BODY_READY_TO_COPY.md`, thêm evidence build/test, yêu cầu collaborator review và gửi link PR cho nhóm. Không ghi nhận CI/review là pass cho đến khi link GitHub thật thể hiện trạng thái đó.

## Phối hợp nhóm

TV2 tiếp tục xử lý PR #7 trên branch `feature/domain-foundation-refactor` theo hai review comments và chờ CI/review lại; TV3 không được thao tác hay nhận phần commit của TV2. TV4 cũng tạo branch mới từ `dev`, làm phần được phân công, commit/push/PR bằng identity của TV4. Nhóm trưởng chỉ đánh giá các PR hợp lệ, có đóng góp code thật, description, test/build phù hợp, CI và cross-review; sau đó mới quyết định merge feature vào `dev` và chọn nội dung đưa tiếp vào `main` release.
