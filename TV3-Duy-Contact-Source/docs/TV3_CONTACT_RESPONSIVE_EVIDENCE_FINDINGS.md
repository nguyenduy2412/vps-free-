# Kết quả evidence responsive 360px — Contact Request TV3

## Điều kiện tạo evidence

Evidence được tạo ngày chạy local bằng Playwright với `CAPTURE_CONTACT_EVIDENCE=1`, viewport `mobile-360-chromium` là **360 × 800** và `desktop-chromium`. Trang Admin dùng một session Admin kỹ thuật và một record `contact-evidence@example.test` được mock tại browser layer; không gọi Docker/SQL Server/backend thật.

## Kết quả kiểm tra trực quan

| Ảnh | Kết quả quan sát | Phân loại evidence |
|---|---|---|
| `contact-public-mobile-360-chromium.png` | Hero, các field form, textarea và CTA hiển thị trong chiều rộng 360px; không thấy tràn ngang cấp page. | UI local/mock. |
| `contact-admin-mobile-360-chromium.png` | Route `/admin/contact-requests` render sau hunk navigation; banner, filter, record kỹ thuật và vùng chi tiết đều hiển thị. Table được bọc vùng scroll ngang nội bộ như thiết kế, page không tràn ngang. | UI local/mock. |
| `contact-public-desktop-chromium.png` | Có ảnh desktop để đối chiếu layout public. | UI local/mock. |
| `contact-admin-desktop-chromium.png` | Có ảnh desktop để đối chiếu layout Admin Contact. | UI local/mock. |

Playwright chạy **4/4 pass** cho desktop và 360px; mỗi test assert `document.documentElement.scrollWidth <= window.innerWidth` trước khi chụp ảnh.

> Các ảnh này chứng minh layout frontend ở browser với data/session kỹ thuật mock. Chúng **không** chứng minh migration, Docker, SQL Server, API authorization, rate limit hoặc staging thật. Các gate đó vẫn phải chạy trên clone `dev`/hạ tầng thật theo `TV3_CONTACT_LOCAL_SMOKE_E2E_360_RUNBOOK.md` và checklist staging.
