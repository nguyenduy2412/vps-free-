# Phân tích coverage — 214 backend tests

> **Historical snapshot:** báo cáo này ghi nhận lần chạy 214 test trước khi bổ sung test Contact coverage. Evidence hiện hành là `TV3_CONTACT_COVERAGE_220_IMPROVEMENT.md`: 220/220 pass, Contact source 98.49%, `ContactRequestService.cs` và `ContactRequestRepository.cs` đều 100% line coverage.

## Kết quả nguồn dữ liệu

Lần chạy `dotnet test CloudServiceStore.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` tạo 3 Cobertura XML và pass **214/214** test: Domain 22/22, Application 117/117, Integration 75/75.

| Chỉ số | Kết quả | Cách diễn giải đúng |
|---|---:|---|
| Tổng thô của 3 root report | 22.25% line, 48.99% branch | **Không dùng làm KPI tổng**: report từ các test assemblies lặp source classes và gồm migration/model snapshot generated chưa được thực thi. |
| Source meaningful đã hợp nhất line hit, loại migration/model snapshot | **86.38% line** (4,286/4,962) | Thước đo dùng cho ưu tiên tối ưu source nghiệp vụ. |
| Source Contact đã hợp nhất | **89.46% line** (416/465) | Module TV3 Contact đang có coverage nghiệp vụ tốt; tối ưu tiếp tập trung các nhánh còn thiếu dưới đây. |

> Không nên viết dummy tests chỉ để bao phủ migration `.Designer.cs`, `ModelSnapshot` hay factory design-time. Chúng kéo tổng thô xuống nhưng không làm tăng chất lượng regression business/API.

## Điểm cần ưu tiên trong phạm vi Contact TV3

| Ưu tiên | File | Coverage line hợp nhất | Phần còn thiếu | Test nên bổ sung |
|---|---|---:|---|---|
| P1 | `ContactRequestContracts.cs` | 73.08% | Mapping `ContactRequestDetailDto`/`ContactRequestStatusHistoryDto`. | Service `GetByIdAsync` trả entity có history, assert DTO detail/history/status đầy đủ. |
| P1 | `ContactRequestRepository.cs` | 89.36% | Filter `CreatedFrom`, `CreatedTo`; `FindAsync` include history. | InMemory repository test với date boundary và detail history. |
| P1 | `ContactRequestService.cs` | 89.45% | GetById mapping, một số `CanTransition`/validation branch. | Theory valid/invalid transitions, `GetById` not-found/detail history. |
| P2 | `ContactRequestsController.cs` | 91.67% | `ContactRequestNotFoundException` mapping trong `Execute`. | Mock service throw not-found qua update status, assert `404 ProblemDetails`. |
| P2 | `ContactRequestEntities.cs` | 90.00% | Constructor/property backing lines do compiler instrumentation. | Không ưu tiên test riêng trừ khi có invariant Domain mới. |

## Điểm thấp toàn solution nhưng ngoài phạm vi TV3 Contact

Các file sau thấp và nên được phân công cho chủ module trước release; TV3 không tự sửa để tránh lấn phạm vi: `LandingContentController` 0%, `PromotionsController` 8%, `CatalogController` 10.71%, `AuthController` 33.78%, `AffiliatesController` 52.94%, `OrdersController` 55.56%, `NewsController` 56% và `EditorWorkspaceController` 57.14%.

Ma trận `TV3_DEV_RELEASE_REGRESSION_ENDPOINT_MATRIX.md` đã liệt kê endpoint regression cho các module này. Team lead cần giao test ownership cho TV1/TV2/TV4 theo bảng phân công, thay vì thêm code/test lẫn vào branch Contact TV3.

## Khuyến nghị trước PR release

1. Giữ 214/214 evidence hiện tại; không hạ chuẩn chỉ vì raw root coverage bị migration/generated code làm nhiễu.
2. Nếu còn thời gian trong scope TV3, thêm 3 test P1: repository date/history, service GetById history, controller not-found mapping. Sau đó chạy lại full coverage để so sánh source Contact.
3. Trước release `dev → main`, dùng coverage source meaningful làm chỉ số kỹ thuật và kiểm tra ma trận endpoint P0/P1 theo owner module.
4. Lưu cả 3 Cobertura XML, command log, commit SHA và môi trường chạy; không chỉ ghi một tỷ lệ % vào PR.
