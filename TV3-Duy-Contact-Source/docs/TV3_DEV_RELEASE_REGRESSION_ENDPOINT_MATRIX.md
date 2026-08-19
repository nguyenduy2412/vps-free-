# Ma trận endpoint regression — `dev` trước PR release vào `main`

> Base URL: `http://localhost:8080/api/v1`. Chạy trên **HEAD mới nhất của `dev`** sau khi mọi feature đã merge. Dùng dữ liệu kỹ thuật/disposable; không tạo testimonial, customer-logo hoặc review giả. Với endpoint có `id`/`slug`, dùng record hợp lệ được tạo hoặc seed rõ nguồn trước khi test.

## Cách đọc ma trận

| Ký hiệu | Ý nghĩa |
|---|---|
| Public | Không token; kiểm tra status thành công và `400` cho payload invalid khi applicable. |
| Customer | Token customer hợp lệ; cũng kiểm tra anonymous `401` và role không phù hợp `403` cho API protected. |
| Policy | Token thỏa policy ghi ở cột Access; thêm negative check `401` anonymous và `403` customer/role không có policy. |
| P0 | Chặn release nếu fail. |
| P1 | Regression bắt buộc trước release; có thể thực hiện bằng suite/collection automation. |

## 0. Availability và authentication — P0

| ID | Method | Endpoint | Access | Regression expected |
|---|---|---|---|---|
| REG-001 | GET | `/health` | Public | `200`; API kết nối được sau migrator. |
| REG-002 | POST | `/auth/register` | Public | `200/201` theo contract với email mới; invalid body `400`. |
| REG-003 | POST | `/auth/login` | Public | Admin/Customer hợp lệ nhận token; sai password `401`; burst login kiểm tra limiter nếu cấu hình. |
| REG-004 | POST | `/auth/refresh` | Public | Refresh hợp lệ; token/refresh invalid bị từ chối. |
| REG-005 | POST | `/auth/logout` | Public | Logout/refresh revoke theo contract; không lộ token. |
| REG-006 | GET | `/auth/me` | Authenticated | Token hợp lệ `200`; anonymous `401`. |
| REG-007 | PUT | `/auth/password` | Authenticated | Đổi password hợp lệ; body sai `400`; token cũ/session behavior đúng policy. |
| REG-008 | PUT | `/auth/profile` | Authenticated | Cập nhật profile hợp lệ; validation `400`; anonymous `401`. |

## 1. Public catalog và promotion — P0/P1

| ID | Method | Endpoint | Access | Priority / expected |
|---|---|---|---|---|
| REG-010 | GET | `/service-categories` | Public | P0; `200`, list public. |
| REG-011 | GET | `/service-categories/{id}` | Public | P1; `200` valid, `404` unknown. |
| REG-012 | GET | `/service-plans` | Public | P0; `200`, paging/query contract nếu có. |
| REG-013 | GET | `/service-plans/{id}` | Public | P1; `200`/`404`. |
| REG-014 | GET | `/service-plans/by-slug/{slug}` | Public | P1; `200`/`404`. |
| REG-015 | GET | `/service-plans/{planId}/qr-code/image` | Public | P1; image/`404` contract, no server error. |
| REG-016 | GET | `/promotions` | Public | P0; `200`, active promotions visible. |
| REG-017 | GET | `/promotions/{id}` | Public | P1; `200`/`404`. |

## 2. Catalog/pricing management — P1

| ID | Method | Endpoint | Access | Regression expected |
|---|---|---|---|---|
| REG-020 | POST | `/service-categories` | `ManageCatalog` | Create valid technical category; `401/403` negative checks. |
| REG-021 | PUT | `/service-categories/{id}` | `ManageCatalog` | Update/validation/`404`. |
| REG-022 | DELETE | `/service-categories/{id}` | `ManageCatalog` | Delete contract and dependency handling. |
| REG-023 | POST | `/service-plans` | `ManageCatalog` | Create valid plan. |
| REG-024 | PUT | `/service-plans/{id}` | `ManageCatalog` | Update/validation/`404`. |
| REG-025 | DELETE | `/service-plans/{id}` | `ManageCatalog` | Delete contract. |
| REG-026 | POST | `/service-plans/{planId}/prices` | `ManagePricing` | Create price, validation/policy. |
| REG-027 | PATCH | `/plan-prices/{priceId}/effective-to` | `ManagePricing` | Effective date update/validation. |
| REG-028 | POST | `/service-plans/{planId}/qr-code` | `ManageQrCodes` | Generate QR, policy/plan `404`. |
| REG-029 | POST | `/promotions` | `ManagePromotions` | Create technical promotion. |
| REG-030 | PUT | `/promotions/{id}` | `ManagePromotions` | Update/validation/`404`. |
| REG-031 | DELETE | `/promotions/{id}` | `ManagePromotions` | Delete contract. |

## 3. Contact Request TV3 — P0

| ID | Method | Endpoint | Access | Regression expected |
|---|---|---|---|---|
| REG-040 | POST | `/contact-requests` | Public | Valid unique request `201`; malformed/short body `400`; duplicate email 24h `409`. |
| REG-041 | POST | `/contact-requests` burst | Public same IP | `429` after `ContactPermitLimit`; use different emails to avoid duplicate `409`. |
| REG-042 | GET | `/contact-requests?page=1&pageSize=2&status=1` | `ManageContactRequests` | Admin/Editor `200`; `totalCount`, status filter and descending paging correct; anonymous `401`, Customer `403`. |
| REG-043 | GET | `/contact-requests/{id}` | `ManageContactRequests` | Detail/history `200`; unknown id `404`; policy negatives. |
| REG-044 | POST | `/contact-requests/{id}/status` | `ManageContactRequests` | `Pending→Contacted→Approved` `200`; invalid/terminal reopen `409`; Rejected/Cancelled no note `400`. |

## 4. Orders, customer account và affiliate — P0/P1

| ID | Method | Endpoint | Access | Priority / expected |
|---|---|---|---|---|
| REG-050 | POST | `/orders` | Customer | P0; create valid order; anonymous `401`, non-Customer `403`. |
| REG-051 | GET | `/orders` | `ManageOrders` | P1; paging/list and role guards. |
| REG-052 | GET | `/orders/{id}` | `ManageOrders` | P1; detail/`404`/policy. |
| REG-053 | PATCH | `/orders/{id}/status` | `ManageOrders` | P1; valid workflow and invalid transition. |
| REG-054 | GET | `/account/orders` | Customer | P1; own records only; anonymous/other role blocked. |
| REG-055 | GET | `/account/orders/{id}` | Customer | P1; own detail; cross-user access blocked. |
| REG-056 | GET | `/account/affiliates` | Customer | P1; own affiliate records only. |
| REG-057 | GET | `/account/affiliates/{id}` | Customer | P1; own detail/cross-user blocked. |
| REG-058 | GET | `/affiliate-program` | Public | P0; `200` public program. |
| REG-059 | GET | `/affiliate-program/admin` | `ManageAffiliateProgram` | P1; policy negatives. |
| REG-060 | PUT | `/affiliate-program` | `ManageAffiliateProgram` | P1; update/validation. |
| REG-061 | POST | `/affiliate-applications` | Customer | P1; create application/validation. |
| REG-062 | GET | `/affiliate-applications` | `ManageAffiliates` | P1; list/policy. |
| REG-063 | GET | `/affiliate-applications/{id}` | `ManageAffiliates` | P1; detail/`404`. |
| REG-064 | PATCH | `/affiliate-applications/{id}/status` | `ManageAffiliates` | P1; valid status + audit/workflow. |

## 5. Landing, customer logo và news — P1

| ID | Method | Endpoint | Access | Regression expected |
|---|---|---|---|---|
| REG-070 | GET | `/landing-content` | Public | `200`, only public/published content. |
| REG-071 | GET | `/landing-content/admin` | `ManageLandingContent` | `200`/policy negatives. |
| REG-072 | PUT | `/landing-content` | `ManageLandingContent` | Update valid approved content; validation. |
| REG-073 | POST/PUT/DELETE | `/testimonials`, `/testimonials/{id}` | `ManageLandingContent` | Use **only consented real data**; no fake review seed. |
| REG-074 | POST/PUT/DELETE | `/customer-logos`, `/customer-logos/{id}` | `ManageLandingContent` | Use approved assets only; policy/validation. |
| REG-075 | GET | `/news-categories` | Public | `200`. |
| REG-076 | GET/POST/PUT/DELETE | `/news-categories/admin`, `/news-categories`, `/news-categories/{id}` | `ManageNews` | Admin list and CRUD policy/validation. |
| REG-077 | GET | `/news-articles`, `/news-articles/{slug}` | Public | Paging/search/category and public detail `200/404`. |
| REG-078 | GET/POST/PUT/DELETE | `/news-articles/admin`, `/news-articles/admin/{id}`, `/news-articles`, `/news-articles/{id}` | `ManageNews` | Admin detail/CRUD/validation. |
| REG-079 | PATCH | `/news-articles/{id}/publish`, `/unpublish`, `/featured` | `ManageNews` | Lifecycle/authorization. |
| REG-080 | POST | `/news-articles/sync-newsdata` | `ManageNews` | Only with configured key; assert controlled external-provider failure rather than unhandled `500`. |

## 6. Editor workspace, dashboard, audit/export — P1

| ID | Method | Endpoint | Access | Regression expected |
|---|---|---|---|---|
| REG-090 | GET | `/editor-workspace` | `ViewEditorWorkspace` | Paging/status query; anonymous `401`, Customer `403`. |
| REG-091 | GET | `/dashboard/order-summary` | `ViewDashboard` | `200`, policy negatives. |
| REG-092 | GET | `/dashboard/popular-plans` | `ViewDashboard` | `200`, policy negatives. |
| REG-093 | GET | `/dashboard/service-interest` | `ViewDashboard` | `200`, policy negatives. |
| REG-094 | GET | `/audit-logs` | `ViewAuditLogs` | Paging/filter, no full sensitive message leak. |
| REG-095 | GET | `/exports/order-requests.xlsx` | `ExportOrders` | `200` attachment/content type, policy negatives. |

## Release execution order

1. Run automated backend/frontend/E2E suite and `Test-ContactRequestPrePr.ps1` against final `dev` SHA.
2. Run public P0 smoke endpoints, then role/policy negative checks for each protected group.
3. Run Contact P0 flow including `400/409/429`, paging/status/authorization and status history.
4. Run P1 groups from Postman/automated suite with technical data; clean only clearly labeled test records if the environment policy requires it.
5. Store test date, `dev` SHA, environment, test account roles, command/report links and failed-case resolution in release PR. A test not run remains unchecked; do not convert this matrix into a claim that every endpoint has passed.
