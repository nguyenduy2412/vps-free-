# API endpoints Thành viên 3

Base URL chung là `/api/v1`. Các controller dùng DTO/Application Service và trả JSON camelCase theo cấu hình Web API hiện có.

## Landing, testimonial và customer logo

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET | `/landing-content` | Public | Đọc landing content public, chỉ dữ liệu active/published hợp lệ. |
| GET | `/landing-content/admin` | `ManageLandingContent` | Đọc dữ liệu Landing cho quản trị. |
| PUT | `/landing-content` | `ManageLandingContent` | Cập nhật nội dung Landing. |
| POST | `/testimonials` | `ManageLandingContent` | Tạo testimonial. |
| PUT | `/testimonials/{id}` | `ManageLandingContent` | Cập nhật testimonial. |
| DELETE | `/testimonials/{id}` | `ManageLandingContent` | Xóa testimonial. |
| POST | `/customer-logos` | `ManageLandingContent` | Tạo customer logo. |
| PUT | `/customer-logos/{id}` | `ManageLandingContent` | Cập nhật customer logo. |
| DELETE | `/customer-logos/{id}` | `ManageLandingContent` | Xóa customer logo. |

> Dữ liệu testimonial/logo trên trang `/customers` được lấy từ public Landing API. Không seed hoặc tạo review/đánh giá giả trong source/demo.

## News Management

| Method | Endpoint | Quyền | Mục đích |
|---|---|---|---|
| GET | `/news-categories` | Public | Danh sách category public. |
| GET | `/news-categories/admin` | `ManageNews` | Danh sách category quản trị. |
| POST | `/news-categories` | `ManageNews` | Tạo category. |
| PUT | `/news-categories/{id}` | `ManageNews` | Cập nhật category. |
| DELETE | `/news-categories/{id}` | `ManageNews` | Xóa category. |
| GET | `/news-articles` | Public | Bài Published, có query page/pageSize/search/categoryId. |
| GET | `/news-articles/{slug}` | Public | Đọc bài Published theo slug. |
| GET | `/news-articles/admin` | `ManageNews` | Danh sách bài quản trị, gồm draft. |
| GET | `/news-articles/admin/{id}` | `ManageNews` | Chi tiết bài quản trị. |
| POST | `/news-articles` | `ManageNews` | Tạo article Markdown. |
| PUT | `/news-articles/{id}` | `ManageNews` | Cập nhật article. |
| PATCH | `/news-articles/{id}/publish` | `ManageNews` | Publish article. |
| PATCH | `/news-articles/{id}/unpublish` | `ManageNews` | Chuyển article về Draft. |
| PATCH | `/news-articles/{id}/featured` | `ManageNews` | Đặt hoặc bỏ featured. |
| DELETE | `/news-articles/{id}` | `ManageNews` | Xóa article. |
| POST | `/news-articles/sync-newsdata` | `ManageNews` | Đồng bộ NewsData khi biến `NEWSDATA_API_KEY` được cấu hình. |

## Contact Request Management

| Method | Endpoint | Quyền | Mục đích và response chính |
|---|---|---|---|
| POST | `/contact-requests` | Public; named rate policy `contact-requests` | Gửi form tư vấn; `201` khi tạo, `400` validation, `409` duplicate 24 giờ, `429` khi vượt rate limit. |
| GET | `/contact-requests` | `ManageContactRequests` (Admin/Editor) | List, search/filter/pagination. |
| GET | `/contact-requests/{id}` | `ManageContactRequests` | Detail và status history. |
| POST | `/contact-requests/{id}/status` | `ManageContactRequests` | Đổi trạng thái workflow, ghi history/audit; `400`, `404`, `409` theo lỗi nghiệp vụ. |

Workflow hiện có trong source: `Pending → Contacted → Approved`; `Pending/Contacted → Rejected hoặc Cancelled`; ba trạng thái terminal không mở lại. `Rejected` và `Cancelled` bắt buộc note. Public Contact dùng rate limiter theo IP; integration test đặt limit = 2 để xác minh request thứ ba trả `429`.

## Ghi chú bàn giao

Trước PR, chạy backend build/test, test rate limit, SQL Server migration/Docker và frontend lint/build trên clone repository thật. Snapshot hiện tại không có Git repository nên không có commit/PR/review được tạo từ sandbox này.
