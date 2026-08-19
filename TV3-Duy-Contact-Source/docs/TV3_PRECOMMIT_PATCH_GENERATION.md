# Tạo patch diff TV3 trước commit cuối

Source snapshot TV3 dùng để đóng gói **không chứa `.git` hoặc branch `dev`**, nên không thể tạo `git diff` thật trong sandbox. Không dùng diff tự chế hay so source với file rỗng để thay thế baseline `dev`.

Trên **official Git clone** sau khi merge/rebase `dev` mới nhất, chạy từ branch `feature/contact-request-management`:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Export-TV3ContactPreCommitPatch.ps1
```

Script fetch `origin/dev`, fail nếu branch sai hoặc còn untracked file, chạy `git diff --check origin/dev --`, rồi tạo ba artifacts trong `artifacts/pre-commit-contact/`:

| Artifact | Dùng để làm gì |
|---|---|
| `TV3_CONTACT_PRECOMMIT_VS_DEV.patch` | Full binary-safe diff giữa working tree feature và `origin/dev`. |
| `TV3_CONTACT_PRECOMMIT_FILES.md` | Danh sách file thay đổi, phân loại TV3-contact/shared/outside scope. |
| `TV3_CONTACT_PRECOMMIT_SUMMARY.md` | Cờ dừng nếu có file ngoài scope và danh sách shared hunk cần review. |

Sau khi script pass, đọc patch và đối chiếu những shared file với `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`. Sau đó chạy `Test-ContactRequestPrePr.ps1`; chỉ stage các file đã được review. Không commit `.env`, secret, node_modules, `.next`, bin/obj, migration drift hoặc code module khác.
