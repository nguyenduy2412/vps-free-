# Checklist review migration Contact Request trước Docker database rỗng

> Chỉ review migration được sinh bằng `dotnet ef migrations add` trên branch `feature/contact-request-management` lấy từ `dev` thật. Không review hoặc commit migration thủ công từ ZIP/snapshot.

## 1. Gate trước khi sinh migration

- [ ] Clone là Git worktree chính thức, `dev` mới nhất đã fetch/pull và working tree sạch.
- [ ] Source Contact v45 đã copy đúng phạm vi; sáu file shared chỉ merge từng hunk theo `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`.
- [ ] `dotnet restore`, Release build, toàn bộ test, `npm ci`, lint và `NODE_ENV=production npm run build` đều pass trên branch hiện tại.
- [ ] Không tồn tại migration Contact cũ/sai baseline trong staging area; không sửa thủ công migration để che model drift.

## 2. Sinh migration và xem diff

```bash
dotnet ef migrations add AddContactRequestManagement \
  --project src/CloudServiceStore.Infrastructure \
  --startup-project src/CloudServiceStore.WebApi \
  --output-dir Persistence/Migrations

git diff -- src/CloudServiceStore.Infrastructure/Persistence/Migrations
```

- [ ] Migration có timestamp/name mới, được EF sinh tự động trong `Persistence/Migrations`.
- [ ] `Up()` chỉ tạo hoặc thay đổi schema cần thiết cho `ContactRequests`, `ContactRequestStatusHistories`, index Contact và FK Contact.
- [ ] `Down()` chỉ đảo ngược đúng các thay đổi Contact ở `Up()`.
- [ ] Tên bảng, cột, length, nullable và enum/status mapping khớp entity/configuration Contact hiện hành.
- [ ] Index có ý nghĩa truy vấn Contact: queue/list theo `CreatedAt DESC`, status/date, email/date duplicate-window và history contact/date; không thêm index full-text giả cho `Contains` nếu DBA/contract chưa phê duyệt.
- [ ] FK Contact tới AppUser (nếu có) dùng hành vi `Restrict`; history tới Contact dùng `Cascade`; không tạo cascade ngoài thiết kế.
- [ ] Không có dữ liệu seed, password, token, LocalDB path, connection string thật hoặc SQL hard-code trong migration.

## 3. Dấu hiệu phải dừng và sinh lại

Nếu diff có bất kỳ nội dung sau, **không commit, không chạy Docker DB rỗng và không sửa tay để che lỗi**:

| Dấu hiệu | Hành động bắt buộc |
|---|---|
| `CreateTable`/`DropTable` cho `AppUsers`, News, Orders, Promotions, Affiliate hoặc module khác | Xóa migration vừa sinh, đồng bộ/review `dev` và tìm model drift. |
| Thay đổi schema hàng loạt không liên quan Contact | Dừng, xác định hunk shared/baseline gây drift. |
| Migration cũ từ ZIP được copy vào branch | Loại migration cũ; sinh lại từ `dev` thật. |
| Migration compile không được | Sửa source/configuration trước, sau đó xóa migration lỗi và sinh lại. |
| `git diff --check` lỗi whitespace | Sửa lỗi diff trước mọi test database. |

## 4. Cổng Docker database rỗng sau review

Chỉ khi mọi ô trên đạt:

```powershell
.\scripts\Run-ContactRequestEmptyDatabase.ps1 -Reset
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml ps -a
docker compose -f docker-compose.yml -f docker-compose.contact-empty.yml logs --no-color migrator
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

- [ ] SQL Server `healthy`.
- [ ] `migrator` `exited (0)`.
- [ ] API `running`; `/health` HTTP `200`.
- [ ] Test migration SQL Server chỉ dùng database disposable có guard tên `ContactRequestIntegration_`, không phải DB team/dev/staging.
- [ ] Lưu log migrator, `ps`, health result và `git diff` không có secret; không chạy `docker compose down -v` với volume nhóm/staging.
