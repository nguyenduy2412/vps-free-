# Xử lý Git merge conflict — TV3 Contact Request → `dev`

Tài liệu này dùng khi branch `feature/contact-request-management` có conflict hoặc bị GitHub báo behind `dev`. Mục tiêu là tích hợp thay đổi mới từ `dev` **mà không ghi đè code thành viên khác**. Không xử lý conflict trực tiếp trên `main` và không force-push để xóa lịch sử review.

## 1. Chuẩn bị an toàn

Chỉ thao tác trên clone chính thức và đúng identity GitHub của TV3. Bảo đảm working tree sạch trước khi lấy `dev` mới:

```bash
git switch feature/contact-request-management
git status
git add <chỉ-file-TV3-đã-xong>
git commit -m "test(contact): add query paging and status filter coverage"
git fetch origin --prune
git branch --show-current
```

Nếu `git status` còn file thay đổi không muốn commit, dừng lại để commit có ý nghĩa hoặc stash có nhãn. Không dùng `git reset --hard` vì có thể xóa code chưa được backup.

## 2. Đồng bộ `dev` vào feature branch

Với branch đã push/mở PR, ưu tiên merge `origin/dev` để tránh rewrite commit mà reviewer đang xem:

```bash
git merge origin/dev
```

Nếu không có conflict, chạy pre-PR check rồi push commit merge bình thường:

```powershell
.\scripts\Test-ContactRequestPrePr.ps1
```

```bash
git push origin feature/contact-request-management
```

## 3. Khi Git báo conflict

Liệt kê chính xác file chưa resolve:

```bash
git status
git diff --name-only --diff-filter=U
git diff -- <file-conflict>
```

Mở từng file có marker `<<<<<<<`, `=======`, `>>>>>>>`. Không dùng `git checkout --ours` hoặc `--theirs` hàng loạt. Đọc cả hai phía và merge thủ công theo bảng sau.

| Loại file conflict | Cách xử lý đúng |
|---|---|
| File Contact TV3 riêng (`ContactRequest*`, UI Contact, test Contact, script Contact) | Giữ implementation Contact mới nhất, đồng thời xem thay đổi `dev` có convention/namespace/API chung cần áp dụng hay không. |
| `CloudServiceStoreDbContext.cs` | Giữ mọi DbSet của `dev`; chỉ bảo đảm hai DbSet Contact của TV3 xuất hiện **một lần**. |
| `DependencyInjection.cs` | Giữ registrations của các module khác; chỉ thêm registration Contact ở section tương ứng, không thay cả file. |
| `Program.cs` | Giữ pipeline/policy/rate limiter từ `dev`; chèn hunk `AddContactRequestRateLimiting` và `ManageContactRequests` đúng vị trí, một lần. |
| `appsettings.json` | Giữ keys của mọi module; chỉ thêm/giữ `RateLimiting:ContactPermitLimit` và seed flag Contact cần thiết; không đưa password/connection string thật. |
| `frontend/src/lib/api.ts` | Giữ API client/types hiện hành; thêm Contact types và `contactRequestsApi` không tạo fetch wrapper trùng. |
| `docker-compose.yml` | Giữ services/volumes/ports của `dev`; chỉ merge hunk Contact seed/migrator healthcheck đã được guide ghi rõ. Không thay image, volume, password hay compose project của nhóm. |
| Migration | Không giải quyết bằng copy migration cũ. Xóa/không stage migration drift, sau đó sinh mới từ `dev` đã merge theo runbook. |

Sau khi xóa đủ marker và kiểm tra logic, stage **từng file đã review**:

```bash
git add <file-da-resolve-1> <file-da-resolve-2>
git diff --cached --check
git diff --cached --name-only
git merge --continue
```

Nếu phát hiện merge đi sai hướng trước khi commit, dùng:

```bash
git merge --abort
```

Lệnh này quay lại trước lần merge hiện tại mà không xóa lịch sử/working change trước đó; không dùng `git reset --hard`.

## 4. Kiểm tra sau conflict và cập nhật PR

Sau merge, chạy đầy đủ:

```powershell
.\scripts\Test-ContactRequestPrePr.ps1
```

Sau đó kiểm tra scope trước push:

```bash
git diff origin/dev...HEAD --check
git diff origin/dev...HEAD --name-only
git log --oneline origin/dev..HEAD
git push origin feature/contact-request-management
```

Trong PR, để lại comment ngắn nêu: `Merged latest dev at <SHA>; resolved <tên file shared nếu có>; reran Test-ContactRequestPrePr.ps1; evidence path <path>.` Request lại review khi conflict ảnh hưởng code reviewer đã xem. Không merge `dev` vào `main` từ branch TV3.
