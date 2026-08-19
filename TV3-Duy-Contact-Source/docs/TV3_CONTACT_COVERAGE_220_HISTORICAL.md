# Contact coverage improvement — 220 backend tests

## Quyết định API và controller

Ba cải tiến controller tương thích đã được áp dụng trong `ContactRequestsController.cs`:

| Điểm best practice | Implementation hiện tại | Regression evidence |
|---|---|---|
| `GetById` dùng error wrapper | `GetById` gọi `Execute(...)`, nên conflict/validation/not-found dùng chung `ProblemDetails` mapping. | Controller test conflict `409`; existing null detail `404`; success `200`. |
| Claim actor không hợp lệ | `Execute` catch `UnauthorizedAccessException` và trả `401 Unauthorized`, service không bị gọi. | Controller test missing `NameIdentifier` claim. |
| Rate limit OpenAPI | Public create có `[ProducesResponseType(typeof(ProblemDetails), 429)]`. | Reflection test metadata `429`. |
| HTTP status update | **Giữ `[HttpPost("{id:guid}/status")]`**. | Contract TV3 đã chốt POST; đổi PATCH sẽ phá api client, Postman/E2E và vi phạm yêu cầu không dùng contract PATCH cũ. |

## Dòng chưa coverage của lần 214 test và test bổ sung

| File | Dòng chưa coverage lần 214 | Test mới đã thêm | Kết quả lần 220 |
|---|---|---|---|
| `ContactRequestService.cs` | 98–104 | `Get_by_id_maps_ordered_history_and_returns_null_for_empty_id` | `GetByIdAsync` empty-id/null, detail fields và history theo thời gian. |
| `ContactRequestService.cs` | 113–122 | `Update_status_rejects_invalid_id_actor_enum_and_note_length_before_repository_call` | ID/actor rỗng, enum ngoài range, note >1000; không gọi repository. |
| `ContactRequestService.cs` | 207–208, 227–228, 233–234 | `Create_rejects_invalid_name_company_and_subject_boundaries` | Name <2, company >160, subject <3. |
| `ContactRequestService.cs` | 252–253, 256–257, 263–264 | `Get_rejects_long_search_invalid_status_and_inverted_date_range` | Search >256, enum invalid, `CreatedTo < CreatedFrom`. |
| `ContactRequestRepository.cs` | 40, 43 | `Admin_list_filters_created_date_range` | `CreatedFrom`/`CreatedTo` query filter. |
| `ContactRequestRepository.cs` | 58–60 | `Admin_detail_includes_status_history` | `FindAsync` include history qua API detail. |

## Kết quả thật sau bổ sung

| Hạng mục | Kết quả |
|---|---|
| Release build | Pass, 0 warning / 0 error. |
| Domain tests | 22/22 pass. |
| Application tests | 121/121 pass. |
| Integration tests | 77/77 pass. |
| Tổng backend tests | **220/220 pass**. |
| Meaningful source coverage | 87.22% line (4,328/4,962), sau khi loại migration/generated code và hợp nhất source line lặp giữa report. |
| Contact source coverage | **98.49% line** (458/465). |
| `ContactRequestService.cs` | **100% line**. |
| `ContactRequestRepository.cs` | **100% line**. |

Coverage còn thiếu trong Contact nằm ở `ContactRequestEntities.cs` 90% (compiler/property instrumentation) và `ContactRequestsController.cs` 91.67% (nhánh `ContactRequestNotFoundException` trong `Execute`). Đây không ngăn các mục tiêu Service/Repository đã vượt 90%; test tiếp theo hợp lý là controller update status throw not-found `404`, không cần sửa contract/status verb.
