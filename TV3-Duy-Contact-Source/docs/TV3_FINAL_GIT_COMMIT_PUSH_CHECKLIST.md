# Checklist cuối Git commit và push — TV3 Contact Request

Tài liệu này áp dụng cho **official Git clone** của nhóm. Không chạy trong archive TV3-only vì archive không có `.git`, `origin` hay baseline `dev`. Chỉ TV3/Duy dùng **tài khoản GitHub và email GitHub đã xác minh của chính mình**; không đổi author commit cũ và không dùng identity của thành viên khác.

## A. Trước khi stage

| Kiểm tra | Lệnh / điều kiện đạt |
|---|---|
| Đúng clone/remote | `git remote -v` hiển thị remote nhóm thật; không dùng ZIP làm repository. |
| Đúng branch | `git branch --show-current` phải là `feature/contact-request-management`. |
| Đồng bộ dev | `git fetch origin --prune`, sau đó merge/rebase `origin/dev` theo `TV3_GIT_MERGE_CONFLICT_GUIDE.md`; không force push. |
| Đúng author | `git config --get user.name` và `git config --get user.email` là tên/email GitHub thật của TV3. Nếu sai, chỉ sửa local repo: `git config user.name "<ten GitHub that cua TV3>"`; `git config user.email "<email GitHub da verify cua TV3>"`. |
| Migration | Sinh migration Contact-only từ latest `dev`; chạy `Test-ContactRequestMigrationSafety.ps1`; review `Up/Down`. |
| Patch scope | Chạy `Export-TV3ContactPreCommitPatch.ps1`. Summary không có `outside-tv3-scope`; shared files chỉ giữ hunk Contact cần thiết. |
| Test/evidence | `Test-ContactRequestPrePr.ps1` pass; Docker empty DB và frontend Contact E2E có evidence thật khi môi trường sẵn sàng. |

> Không commit/claim pass Docker, migration SQL Server, CI hoặc review nếu chưa có log, report và GitHub run thực tế.

## B. Stage có chủ đích

Review trước bằng các lệnh:

```powershell
git status --short
git diff --check origin/dev --
git diff --name-only origin/dev --
git diff origin/dev --
```

Stage theo nhóm file Contact, không dùng `git add .` nếu chưa đọc file manifest từ pre-commit patch:

```powershell
git add src/CloudServiceStore.Domain/Entities/ContactRequestEntities.cs
git add src/CloudServiceStore.Domain/Enums/ContactRequestStatus.cs
git add src/CloudServiceStore.Application/ContactRequests
git add src/CloudServiceStore.Infrastructure/Persistence/Configurations/ContactRequestConfigurations.cs
git add src/CloudServiceStore.Infrastructure/Persistence/ContactRequestRepository.cs
git add src/CloudServiceStore.WebApi/Controllers/ContactRequestsController.cs
git add src/CloudServiceStore.WebApi/Security/ContactRequestRateLimitExtensions.cs
git add frontend/src/app/contact frontend/src/app/admin/contact-requests
git add frontend/src/components/contact-public-client.tsx frontend/src/components/admin-contact-requests-client.tsx
git add frontend/src/lib/contact-request-status.ts frontend/e2e/contact-public.spec.ts frontend/playwright.config.ts
git add tests/CloudServiceStore.Application.Tests/ContactRequestServiceTests.cs
git add tests/CloudServiceStore.Integration.Tests/ContactRequest*.cs
git add tests/postman/CloudServiceStore_TV3*.json
git add scripts/Build-And-AddContactRequestMigration.ps1 scripts/Run-ContactRequestEmptyDatabase.ps1
git add scripts/Start-ContactRequestDemo.ps1 scripts/Test-ContactRequestPrePr.ps1
git add scripts/Test-ContactRequestMigrationSafety.ps1 scripts/Export-TV3ContactPreCommitPatch.ps1
git add docs/CONTACT_REQUEST_*.md docs/TV3_*.md

# Chỉ sau khi dotnet ef sinh và review migration Contact-only:
git add src/CloudServiceStore.Infrastructure/Persistence/Migrations/<timestamp>_AddContactRequestManagement.cs
git add src/CloudServiceStore.Infrastructure/Persistence/Migrations/<timestamp>_AddContactRequestManagement.Designer.cs
git add src/CloudServiceStore.Infrastructure/Persistence/Migrations/CloudServiceStoreDbContextModelSnapshot.cs
```

Sau stage, bắt buộc xem index thay vì chỉ working tree:

```powershell
git diff --cached --check
git diff --cached --name-only
git diff --cached --stat
git diff --cached
```

Dừng và unstage (`git restore --staged <file>`) nếu thấy `.env`, secret, `node_modules`, `.next`, `bin`, `obj`, `TestResults`, archive `.zip`, file Word/PDF nặng, file ngoài Contact TV3, migration tạo bảng module khác, hoặc full replacement shared file.

## C. Commit và push

Khi index chỉ còn Contact TV3 đã review, commit bằng message Conventional Commit đúng phạm vi:

```powershell
git commit -m "feat(contact): implement contact request management"
git show --stat --oneline HEAD
git show --format=fuller --no-patch HEAD
git push -u origin feature/contact-request-management
```

Nếu migration được sinh sau commit code và nhóm muốn tách commit, dùng commit thứ hai thực chất:

```powershell
git commit -m "feat(contact): add contact request migration"
git push
```

Không sửa author của commit cũ, không `git push --force`, không commit rỗng, không push trực tiếp `main` hoặc `dev`.

## D. Mở PR vào dev

Tạo PR **base `dev` ← compare `feature/contact-request-management`**. Dán nội dung từ `TV3_PR_DESCRIPTION_DETAILED.md`, đính kèm test commands/report thật và nêu rõ blocker chưa có evidence nếu còn. Request **TV2** review theo bảng phân công; không coi Copilot hoặc review trống là cross-review đạt.

Chỉ merge khi commit mới nhất có CI xanh, comment/review blocking đã xử lý, diff/migration/Docker evidence hợp lệ và team lead đồng ý. `main` chỉ nhận PR release do nhóm trưởng quyết định sau khi `dev` regression pass.
