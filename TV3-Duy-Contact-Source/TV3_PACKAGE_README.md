# TV3/Duy — Contact Request source package

Root package chuẩn là `TV3-Duy-Contact-Source/`. Đây là source **TV3-only** cho `feature/contact-request-management`, không phải repository Git và không được copy đè toàn bộ source vào `dev`.

Đọc theo thứ tự:

1. `docs/TV3_CONTACT_PR_SCOPE_AND_EVIDENCE_POLICY.md` — phân biệt source PR với raw evidence.
2. `docs/TV3_SHARED_FILE_EXACT_MERGE_GUIDE.md` — merge hunk shared, gồm navigation Admin Contact.
3. `docs/TV3_CONTACT_ONLY_MIGRATION_DEV_EXECUTION.md` — sinh/review migration Contact-only từ `dev` thật.
4. `docs/TV3_V47_DOD_EVIDENCE_MATRIX.md` — evidence snapshot và gate còn blocked.
5. `docs/TV3_CONTACT_DETAILED_SOURCE_MANIFEST.md` — manifest file/source/test/script TV3-only.
6. `docs/TV3_CONTACT_ARCHITECTURE_API_WORKFLOW_MATRIX.md` — kiến trúc, API, workflow, validation, authorization và acceptance gate.
7. `docs/TV3_CONTACT_DEMO_TROUBLESHOOTING_PLAYBOOK.md` — kịch bản demo 7 phút, pre-demo và xử lý sự cố.
8. `docs/TV3_CONTACT_MIGRATION_DOCKER_RECOVERY_GUIDE.md` — chẩn đoán và recovery EF migration/Docker DB rỗng theo từng lỗi.
9. `docs/TV3_V53_EXECUTION_GATE_STATUS.md` — phân biệt package audit với năm execution gate chưa được phép tick Done.

Contract status đã chốt: `POST /api/v1/contact-requests/{id}/status`. Không đổi endpoint này thành PATCH theo tài liệu cũ.
