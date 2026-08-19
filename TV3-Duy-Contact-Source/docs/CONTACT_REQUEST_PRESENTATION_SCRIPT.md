# Lời thoại thuyết trình — Contact Request Management

## Slide 1 — Bài toán và phạm vi

“Em phụ trách module Contact Request thuộc phần TV3. Mục tiêu là tiếp nhận yêu cầu tư vấn từ khách công khai, sau đó để Admin hoặc Editor theo dõi, phân loại và cập nhật kết quả xử lý. Module không thay thế CRM; nó giải quyết luồng liên hệ tối thiểu, có audit và phân quyền.”

## Slide 2 — Kiến trúc

“Module bám Clean Architecture của đồ án. Domain định nghĩa ContactRequest và lịch sử trạng thái; Application chứa DTO, validation và workflow; Infrastructure dùng EF Core/SQL Server; Web API cung cấp bốn endpoint; frontend sẽ dùng proxy Next.js. Không có controller nào truy cập DbContext trực tiếp.”

## Slide 3 — Dữ liệu và bảo mật

“Dữ liệu lưu họ tên, email, số điện thoại, công ty, chủ đề và nội dung. Không lưu mật khẩu, token hoặc dữ liệu thanh toán. AppUserId nullable để hỗ trợ khách chưa đăng nhập. Audit chỉ lưu action, status và email cần thiết, không ghi toàn bộ nội dung liên hệ.”

## Slide 4 — Workflow

“Một yêu cầu bắt đầu ở New, được chuyển InProgress khi nhân viên tiếp nhận, rồi Resolved hoặc Rejected. Rejected bắt buộc có ghi chú. Hệ thống không cho mở lại trạng thái kết thúc để giữ lịch sử rõ ràng. Mỗi lần đổi trạng thái đều sinh status history và audit log.”

## Slide 5 — API public

“Em gọi POST /api/v1/contact-requests không cần token. Response 201 trả request id và trạng thái New. Nếu gửi lại cùng email trong 24 giờ, service trả 409 để hạn chế spam nghiệp vụ. Validation kiểm tra email, số điện thoại, độ dài trường và message.”

## Slide 6 — Quản trị và phân quyền

“GET list, GET detail và PATCH status chỉ dùng policy ManageContactRequests cho Admin và Editor. Khi dùng tài khoản Customer gọi API quản trị, API trả 403; đây là kiểm soát ở backend chứ không chỉ ẩn nút giao diện.”

## Slide 7 — SQL Server, index và migration

“SQL Server là provider chính thức. Index CreatedAt phục vụ danh sách mới nhất; các index Status/CreatedAt, Email/CreatedAt, AppUserId/CreatedAt và history giúp filter queue, chống duplicate, tra cứu owner và lịch sử. Migration chỉ được tạo sau build/test xanh và được review trước database update.”

## Slide 8 — Test và Docker database rỗng

“Module hiện có 16 test Contact Request cho service, controller và migration test. Kịch bản Docker dùng volume/database ContactRequest riêng, reset có kiểm soát, rồi kiểm tra health. Điều này giúp xác minh migration không phụ thuộc dữ liệu cũ.”

## Slide 9 — Demo trực tiếp

“Em gửi request public, lưu request id, đăng nhập Admin/Editor, lọc trạng thái New, mở detail, chuyển New sang InProgress và Resolved. Sau đó em chỉ ra history, ResolvedBy, ResolvedAt. Cuối cùng em thử Customer gọi API admin và thử chuyển ngược status để chứng minh 403 và 409.”

## Slide 10 — Kết luận

“Kết quả là một module contact có luồng đầy đủ từ public submit đến quản trị, có validation, audit, phân quyền, index, test và Docker. Các bằng chứng cuối cùng cần nộp gồm log build/test/coverage, migration thật, ảnh demo, PR và review chéo của nhóm.”
