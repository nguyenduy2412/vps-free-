# Chính sách scope PR và evidence — TV3 Contact Request

## Source PR cần review

PR `feature/contact-request-management` chỉ nên chứa source Contact, test Contact, migration **sinh từ `dev` thật**, và tài liệu vận hành cần thiết. Mỗi shared file chỉ mang hunk được mô tả trong `TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md`; không thay nguyên file.

| Nhóm được phép trong PR | Ví dụ |
|---|---|
| Domain/Application/Infrastructure/Web API Contact | entity/enum, contracts/service, configuration/repository, controller, rate-limit extension. |
| Frontend Contact | `/contact`, `/admin/contact-requests`, Contact API client/types/status metadata và E2E Contact. |
| Test Contact | service/controller/query/rate-limit/migration test và Postman TV3. |
| Migration thật | Migration được EF sinh trên `dev` và đã review Contact-only. |
| Docs tối thiểu | README Contact, shared-hunk guide, migration runbook hoặc PR description theo quy trình nhóm. |

## Không commit raw evidence mặc định

Không đưa raw log, Cobertura XML, TRX, `test-results`, Playwright report, trace, screenshot mock hoặc toàn bộ `artifacts/` vào feature branch, trừ khi quy trình nhóm/giảng viên ghi rõ bắt buộc. Chúng dễ làm PR khó review, không thay CI và có thể chứa metadata môi trường.

| Loại evidence | Nơi phù hợp | Ghi chú |
|---|---|---|
| Raw build/test/coverage log | CI artifact hoặc thư mục evidence ngoài branch | Kèm SHA và lệnh chạy. |
| Trace/screenshot failure | CI artifact/ticket debug | Không dùng làm chứng cứ pass. |
| Screenshot UI mock 360px | Hồ sơ nộp/evidence folder ngoài PR | Phải gắn nhãn local/mock. |
| Docker/migration/health log | CI artifact hoặc hồ sơ demo | Chỉ tạo sau khi chạy hạ tầng thật. |

## Package source TV3-only

Archive delivery dùng root chuẩn `TV3-Duy-Contact-Source/`. Archive không phải Git branch, không chứa `.git`, secret, generated migration cũ hay raw evidence. Nó là nguồn để copy non-shared Contact và merge hunk thủ công vào clone `dev` thật.

> Evidence snapshot đã chạy vẫn được ghi trong `TV3_V47_DOD_EVIDENCE_MATRIX.md`, nhưng không được dùng thay cho PR CI, migration Docker, review collaborator hay lịch sử Git thật.
