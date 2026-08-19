# Ma trận tuân thủ kỹ thuật cuối — TV3 Contact Request

## Kết luận chính xác

Code TV3 **tuân thủ tĩnh và có evidence build/test** cho các quy ước kỹ thuật thuộc phạm vi Contact. Tuy nhiên, không thể tuyên bố **tuyệt đối hoàn tất Definition of Done/PR readiness** cho đến khi migration Contact-only, Docker database rỗng, patch `feature → dev`, CI và TV2 review được thực thi/ghi nhận trên official Git clone và GitHub thật.

| Quy ước bắt buộc | Trạng thái hiện tại | Evidence hoặc hành động còn lại |
|---|---|---|
| Scope TV3 Contact-only | Pass tĩnh. | 27 source/UI/test/script; archive scan không có module TV khác. |
| Clean Architecture | Pass tĩnh. | Controller không truy cập DbContext; service không dùng EF query API; repository dùng EF; DTO records không trả entity. |
| REST/base URL/DTO/paging | Pass tĩnh. | `/api/v1/contact-requests`, `PagedResult<T>`, `ProblemDetails`, DTO riêng. |
| Status contract | Pass tĩnh. | Giữ POST status hiện hành; không dùng PATCH contract cũ. |
| Authorization/rate limit | Pass tĩnh + test. | `ManageContactRequests`, public create IP policy; 401/403/429 metadata và regression tests. |
| Validation/audit | Pass tĩnh + test. | DataAnnotations `param:` + service defense-in-depth; create/status audit/history. |
| Frontend convention | Pass tĩnh + prior lint/build/E2E evidence. | Central `contactRequestsApi`; không direct fetch/localStorage/sessionStorage/hard-code localhost trong Contact UI. |
| Release build/backend tests | Pass thật. | Build 0 warning/0 error; Domain 22/22, Application 121/121, Integration 80/80 = 223/223. |
| Contact coverage | Pass thật. | Contact source 98.49%; Service/Repository 100% line. |
| Contact source formatting | Pass thật. | Scoped `dotnet format` Application, Contact controller, Integration Tests. |
| Full-solution formatting | External blocker. | `ReportingController.cs:52` whitespace thuộc TV4; báo owner/team lead, TV3 không sửa lấn phạm vi. |
| EF migration Contact-only | Pending, required. | Phải sinh từ latest `dev`, review diff, chạy migration safety. |
| Docker empty database | Pending, required. | Cần SQL healthy, migrator exit 0, `/health` 200 trên Docker Desktop. |
| Git patch / PR / CI / TV2 review | Pending, required. | Snapshot không có `.git`/remote; dùng export patch script và thực hiện trên GitHub thật. |

## Quyết định commit

TV3 có thể **chuẩn bị** commit source Contact sau khi hoàn thành những cổng Pending trên clone thật. Không commit ngay từ archive hoặc dùng evidence sandbox thay cho migration/Docker/GitHub evidence. Nếu nhóm cần commit code trước khi migration được sinh, phải ghi rõ PR là chưa sẵn sàng merge và thêm migration Contact-only trong commit tiếp theo trên chính branch, sau đó chạy lại pre-PR checks.
