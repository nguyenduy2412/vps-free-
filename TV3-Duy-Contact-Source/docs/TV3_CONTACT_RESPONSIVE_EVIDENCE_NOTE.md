# Ghi chú evidence responsive Contact Request

File `frontend/e2e/contact-responsive-evidence.spec.ts` chỉ tạo **evidence UI local** khi chạy với `CAPTURE_CONTACT_EVIDENCE=1`. Test mock một record kỹ thuật `@example.test` ở browser layer để trang Admin có dữ liệu hiển thị; không dùng database Docker, token, account thật hoặc dữ liệu khách hàng.

Chạy:

```bash
cd frontend
CAPTURE_CONTACT_EVIDENCE=1 npx playwright test e2e/contact-responsive-evidence.spec.ts
```

Playwright tạo ảnh trong `test-results/`. Chọn tối thiểu ba ảnh để nộp: `/contact` ở `mobile-360-chromium`, `/admin/contact-requests` ở `mobile-360-chromium`, và một ảnh desktop. Trước khi đính kèm hồ sơ, đổi/copy các ảnh được chọn vào thư mục evidence của nhóm và ghi rõ: **local UI mock evidence, không phải bằng chứng Docker/staging hay authorization API thật**.

Mỗi test còn assert `document.documentElement.scrollWidth <= window.innerWidth`. Assertion này chứng minh page không tràn ngang tại viewport test; nó không thay thế kiểm tra thao tác thủ công hoặc E2E/CI trên branch `dev` thật.
