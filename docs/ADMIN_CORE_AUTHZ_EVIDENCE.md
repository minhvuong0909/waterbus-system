# Bằng chứng phân quyền — Admin core (PR #63)

Kiểm tra bằng HTTP thật (không phải unit test) trên API của nhánh `feat/be-admin-core-configuration` chạy trong container với SQL Server thật,
DB tạm (đã xoá), môi trường Development (có seed 3 tài khoản mẫu). Ngày chạy: 06/11/2026.

## Phạm vi và cách chạy

27 endpoint ở 5 controller: `AdminRoutesController` (7), `AdminSchedulesController` (7), `AdminTripsController` (2),
`AdminFaresController` (5), `AdminScannerDevicesController` (6). Tất cả gắn `[Authorize(Policy = "AdminOnly")]`.
Mỗi endpoint được gọi với body `{}` và GUID giả `1111…` ở các tham số đường dẫn, lần lượt bằng 5 người gọi.

## Kết quả mong đợi và đã quan sát

| Người gọi | Mong đợi | Quan sát (27/27 endpoint) |
|---|---|---|
| Không token | 401 | 401 |
| Token ký sai khóa, mang claim role `Admin` (HS256, issuer/audience đúng) | 401 | 401 |
| Captain (đăng nhập thật) | 403 | 403 |
| Staff (đăng nhập thật) | 403 | 403 |
| Admin (đăng nhập thật) | Qua được lớp phân quyền | Không có 401/403 nào |

Tổng số kết quả không như mong đợi: **0**.

## Cách phân biệt "được phép" với "lỗi nghiệp vụ"

Với Admin, tiêu chí đạt là **mã khác 401 và khác 403**. Mã trả về thể hiện kết quả xử lý sau khi đã qua phân quyền, không phải lỗi phân quyền:

- `GET` danh sách → `200`.
- `GET`/`PUT`/`POST …/activate|deactivate` với id không tồn tại → `404` (NotFound).
- `POST`/`PUT` với body `{}` → `400` (validation, có `errors` trong ProblemDetails).

Một endpoint trả 400/404 cho Admin chứng minh request đã tới Service; endpoint trả 403 cho Admin mới là lỗi.

## Giới hạn

- Chưa kiểm vai Passenger/Guest (không có endpoint đăng ký trong build này); các vai đó không có role `Admin` nên cùng nhánh với Captain/Staff.
- Đăng nhập có giới hạn tốc độ (429 sau nhiều lần liên tiếp): lấy token một lần rồi dùng lại.
- Bằng chứng này là một lần chạy tại thời điểm trên; không thay cho test tự động. Nếu thêm endpoint admin mới, chạy lại với cùng cách.
