# Static audit chính xác — `ContactRequestsController`

Một raw audit cũ từng ghi “Controller does not access DbContext: FAIL” do grep quá đơn giản. Kết luận đó là **false positive**: controller hiện inject `IContactRequestService`, không inject `CloudServiceStoreDbContext`, và không dùng EF/SQL query primitive.

Script thay thế là `scripts/Check-ContactControllerArchitecture.sh`. Nó kiểm tra đồng thời hai điều: controller có dependency `IContactRequestService contactRequestService`, và không chứa các symbol truy cập persistence thực tế như `CloudServiceStoreDbContext`, `DbContext`, `Set<`, `AnyAsync`, `Where`, `Include`, `AsNoTracking`, `SaveChanges`, `Database`, `FromSql` hay `SqlConnection`.

```bash
bash scripts/Check-ContactControllerArchitecture.sh
```

Kết quả đạt phải là:

```text
CONTACT_CONTROLLER_ARCHITECTURE=PASS
```

Script cũng kiểm tra contract status đã chốt một lần: `[HttpPost("{id:guid}/status")]` và bốn action delegate sang `IContactRequestService`. Nó không thay thế unit/integration test, review trên branch `dev` hay API smoke Docker; đây chỉ là guard tĩnh để tránh regression kiến trúc rõ ràng.

> Không đưa raw log cũ có false positive vào mô tả PR hoặc báo cáo kết luận. Nếu cần lưu historical evidence, gắn nhãn là “superseded static audit — false positive” và dẫn tới script mới.
