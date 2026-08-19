# Kiểm tra trạng thái PR / CI / Review GitHub — 18/08/2026

## Kết quả kiểm tra có thể xác minh

1. Connector GitHub của session tồn tại nhưng đang **disabled**, nên không có quyền/App connection để đọc dữ liệu repository private qua integration.
2. URL public được cung cấp trước đó: `https://github.com/HieuIPO/CloudServiceStore-Team-Contributions/pulls` hiện trả **GitHub 404 Page not found** khi kiểm tra không đăng nhập.
3. GitHub REST public endpoint `GET /repos/HieuIPO/CloudServiceStore-Team-Contributions` cũng trả HTTP **404** với `{"message":"Not Found"}`. Vì repository không truy cập được, endpoint list PR không được gọi tiếp.
4. Tìm kiếm public theo tên repository/owner không trả về repository thay thế đáng tin cậy.

## Kết luận trung thực

Không thể liệt kê hoặc xác minh danh sách PR hiện tại, trạng thái CI hay review của nhóm từ môi trường này. Đây có thể là repository đã đổi tên, private, bị xóa, hoặc URL không còn đúng; không suy đoán nguyên nhân và không khẳng định trạng thái PR nào từ kết quả 404.

## Cách nhóm xác minh trên repository thật

TV3 hoặc nhóm trưởng, bằng GitHub account có quyền, cần mở repository hiện hành rồi kiểm tra:

```bash
gh pr list --state all --limit 100 \
  --json number,title,state,isDraft,headRefName,baseRefName,author,createdAt,updatedAt,url

gh pr view <PR_NUMBER> --json state,mergeStateStatus,reviews,statusCheckRollup,comments,url
```

Hoặc dùng GitHub web UI: tab **Pull requests** → từng PR → **Checks** và **Reviews**. Chỉ tính PR hợp lệ khi PR có code thật trên feature branch, target `dev`, mô tả/test phù hợp, CI được thực thi và collaborator review thật. Không tính PR upload nhầm/trùng như #1, #3, #6 theo quy định nhóm; không push trực tiếp `main`.

Sau khi nhóm cung cấp URL repository hiện hành hoặc bật GitHub connector có quyền, có thể chạy lại audit để lập bảng PR/CI/review từ dữ liệu thật.
