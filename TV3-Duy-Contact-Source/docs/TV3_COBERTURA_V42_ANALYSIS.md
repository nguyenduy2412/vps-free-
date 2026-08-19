# Phân tích Cobertura trong ZIP TV3 v42

## Phạm vi evidence

ZIP `TV3-Duy-Contact-Full-Source-Compatible-Updates-v42.zip` chứa ba report `coverage.cobertura.xml`, file phân tích chuẩn hóa `coverage-analysis-223.json/.log`, và report cải tiến lịch sử sau mốc 220 test. Báo cáo này dùng các artifact trong ZIP v42; không coi staging/Docker/GitHub là đã pass.

## 1. Chỉ số raw của từng Cobertura XML

| Report Cobertura | Line rate raw | Branch rate raw | Diễn giải |
|---|---:|---:|---|
| `c8a7fcce-905b-4ac4-ae5c-853fcde23a61` | 53.60% | 100.00% | Một assembly test; không dùng đơn lẻ làm KPI solution. |
| `d8529c16-c14d-4129-8d18-d81eab08a428` | 80.12% | 62.64% | Một assembly test; có source trùng với report khác. |
| `f4ea203c-ac77-49c2-ab3d-64aad75bb373` | 15.75% | 42.89% | Có generated/migration và source module khác chưa phải scope TV3. |

> Không cộng hoặc lấy trung bình trực tiếp ba line rate raw. Ba report đến từ các test assemblies khác nhau, có source class/line lặp; migration/model snapshot sinh tự động cũng làm chỉ số raw lệch khỏi mức coverage nghiệp vụ thực tế.

## 2. Coverage đã chuẩn hóa

Script phân tích `coverage-analysis-223` hợp nhất line-hit trùng, loại migration/generated source và tính trên source nghiệp vụ. Kết quả cần dùng cho assessment là:

| Phạm vi | Line coverage | Trạng thái |
|---|---:|---|
| Source nghiệp vụ solution đã chuẩn hóa | **87.22%** (4,328/4,962) | Không phải toàn bộ là scope TV3. |
| Source Contact TV3 | **98.49%** (458/465) | Vượt mục tiêu module >90%. |
| `ContactRequestService.cs` | **100%** | Validation, duplicate, workflow, history, query guard đã có test. |
| `ContactRequestContracts.cs` | **100%** | DTO mapping/validation contract được phủ. |
| `ContactRequestRepository.cs` | **100%** | Filter, paging, date range, detail/history có test. |

## 3. Kết quả test liên quan

Evidence mới nhất trong bundle TV3 ghi **223/223 backend tests pass**: Domain 22/22, Application 121/121 và Integration 80/80. Report `TV3_CONTACT_COVERAGE_220_HISTORICAL.md` được giữ để truy vết các test thêm ở mốc 220; không dùng số 220 thay cho evidence 223 hiện hành.

## 4. Kết luận và hành động

Coverage Contact đủ cho pre-PR về mặt Service/Repository. Các điểm coverage solution thấp thuộc source generated hoặc module khác không được TV3 sửa lấn phạm vi. Trước PR, chạy lại `Test-ContactRequestPrePr.ps1` trên official Git clone để sinh coverage gắn SHA commit thực tế; không dùng report ZIP thay evidence CI/PR cuối cùng.
