# Áp dụng source ZIP TV3 vào branch feature hiện tại

Tài liệu này dùng cho official Git clone. **Không chạy lệnh Git trong file ZIP**; giải nén ZIP vào thư mục tạm ngoài repository trước, rồi chỉ copy source Contact TV3 theo manifest.

## 1. Đồng bộ branch hiện tại

```powershell
git status --short
git fetch origin --prune
git branch --show-current

# Nếu chưa ở branch TV3, tạo từ dev mới nhất:
git switch dev
git pull --ff-only origin dev
git switch -c feature/contact-request-management

# Nếu branch TV3 đã tồn tại, giữ branch đó rồi merge dev:
# git switch feature/contact-request-management
# git merge origin/dev
```

Dừng nếu `git status --short` có thay đổi không thuộc TV3. Không dùng `git reset --hard`, không force push và không checkout/copy trực tiếp vào `main`.

## 2. Giải nén và copy file Contact-only

```powershell
$zip = "$HOME\Downloads\TV3-Duy-Contact-Full-Source-Compatible-Updates-v41.zip"
$extract = "$env:TEMP\TV3-Contact-v41"
Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Path $zip -DestinationPath $extract -Force

$bundle = Get-ChildItem $extract -Directory | Select-Object -First 1
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.Domain\Entities\ContactRequestEntities.cs" ".\src\CloudServiceStore.Domain\Entities\ContactRequestEntities.cs"
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.Domain\Enums\ContactRequestStatus.cs" ".\src\CloudServiceStore.Domain\Enums\ContactRequestStatus.cs"
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.Application\ContactRequests" ".\src\CloudServiceStore.Application\ContactRequests" -Recurse -Force
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.Infrastructure\Persistence\Configurations\ContactRequestConfigurations.cs" ".\src\CloudServiceStore.Infrastructure\Persistence\Configurations\ContactRequestConfigurations.cs"
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.Infrastructure\Persistence\ContactRequestRepository.cs" ".\src\CloudServiceStore.Infrastructure\Persistence\ContactRequestRepository.cs"
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.WebApi\Controllers\ContactRequestsController.cs" ".\src\CloudServiceStore.WebApi\Controllers\ContactRequestsController.cs"
Copy-Item "$($bundle.FullName)\src\CloudServiceStore.WebApi\Security\ContactRequestRateLimitExtensions.cs" ".\src\CloudServiceStore.WebApi\Security\ContactRequestRateLimitExtensions.cs"
Copy-Item "$($bundle.FullName)\tests\CloudServiceStore.Application.Tests\ContactRequestServiceTests.cs" ".\tests\CloudServiceStore.Application.Tests\ContactRequestServiceTests.cs"
Copy-Item "$($bundle.FullName)\tests\CloudServiceStore.Integration.Tests\ContactRequest*.cs" ".\tests\CloudServiceStore.Integration.Tests\"
Copy-Item "$($bundle.FullName)\scripts\Test-ContactRequest*.ps1" ".\scripts\"
Copy-Item "$($bundle.FullName)\scripts\Export-TV3ContactPreCommitPatch.ps1" ".\scripts\"
Copy-Item "$($bundle.FullName)\docs\TV3_*.md" ".\docs\"
```

Copy frontend Contact routes/components/status metadata, E2E, Postman and staging override only nếu source baseline hiện có đúng đường dẫn; xem `TV3_FINAL_EXECUTION_RUNBOOK.md` và manifest trước khi copy toàn bộ folder frontend.

## 3. Shared files: chỉ merge hunk

**Không copy/ghi đè** toàn bộ các file sau từ ZIP: `CloudServiceStoreDbContext.cs`, `DependencyInjection.cs`, `Program.cs`, `appsettings.json`, `frontend/src/lib/api.ts`, `docker-compose.yml`, `DatabaseInitializer.cs`, `AuthSecurityExtensions.cs`.

Mở `docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`; chỉ thêm hunk Contact khi missing trên `dev`. Sau đó kiểm tra:

```powershell
git diff --check
git diff --name-only origin/dev --
git diff --name-only origin/dev -- | Select-String 'News|Landing|Customer|Affiliate|Order|AuthSecurity|site-header|admin-nav'
```

Nếu lệnh cuối có output, dừng và bỏ file ngoài scope khỏi working tree trước khi tiếp tục.

## 4. Migration, test và stage

```powershell
.\scripts\Build-And-AddContactRequestMigration.ps1
.\scripts\Test-ContactRequestMigrationSafety.ps1
.\scripts\Export-TV3ContactPreCommitPatch.ps1
.\scripts\Test-ContactRequestPrePr.ps1

git status --short
git add <chi-cac-file-Contact-va-migration-Contact-only-da-review>
git diff --cached --check
git diff --cached --name-only
```

Chỉ sau đó mới commit bằng identity GitHub thật TV3 và push `feature/contact-request-management` lên remote để mở PR vào `dev`.
