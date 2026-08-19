# Preflight Bash cho CORS và Docker Compose staging — TV3 Contact Request

Script `scripts/Check-StagingCorsCompose.sh` là kiểm tra **trước khi deploy**, không khởi động container, không sinh migration và không chạy smoke API. Nó xác nhận biến CORS/frontend, các secret khởi động cần thiết và render Compose bằng `--no-interpolate` để không in giá trị secret.

## Cách chạy trên host staging

Tạo/nhận file environment protected theo cơ chế hạ tầng đang dùng. File đó không được commit, không dùng `source` và không đưa vào evidence. Sau đó chạy từ root repository:

```bash
bash scripts/Check-StagingCorsCompose.sh \
  --env-file /run/secrets/cloud-store-staging.env \
  --frontend-origin https://staging.example.com \
  --next-public-site-url https://staging.example.com \
  --api-base-url http://api:8080 \
  --require-frontend-runtime
```

Nếu database staging mới hoàn toàn và cần bootstrap account kỹ thuật, thêm `--require-bootstrap-admin`. Script chỉ kiểm tra hiện diện của `SEED_ADMIN_PASSWORD`, không hiển thị giá trị.

## Kết quả mong đợi

Khi hợp lệ, output kết thúc bằng:

```text
STAGING_PREFLIGHT=PASS
```

Các kiểm tra gồm `FRONTEND_STAGING_ORIGIN` exact HTTPS origin, mapping CORS trong override, `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY` tối thiểu 32 ký tự, `NEXT_PUBLIC_SITE_URL` bằng CORS origin, định dạng `API_BASE_URL`, hai seed staging `false`, và `docker compose config --no-interpolate`.

> Script không in password, JWT key hoặc rendered Compose đã nội suy. `FRONTEND_STAGING_ORIGIN` không phải secret nhưng vẫn không cần ghi trong PR/evidence nếu hạ tầng không cho phép.

## Chạy không có Docker

Trên máy chỉ cần review input/source, chạy:

```bash
bash scripts/Check-StagingCorsCompose.sh \
  --env-file /run/secrets/cloud-store-staging.env \
  --frontend-origin https://staging.example.com \
  --skip-docker-config
```

Kết quả này chỉ xác nhận input và file cấu hình tĩnh; không chứng minh Compose render hoặc staging hoạt động. Trên host staging thật, bắt buộc chạy lại không có `--skip-docker-config`, sau đó thực hiện `TV3_STAGING_POST_DEPLOY_SMOKE_CHECKLIST.md`.
