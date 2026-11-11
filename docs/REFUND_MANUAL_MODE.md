# Hoàn tiền thủ công có kiểm soát (Manual) và hoàn theo chuyến

Nhóm đã chốt với thầy: **hệ thống không tự động hoàn tiền cho khách**. Admin tạo yêu cầu hoàn, nhân viên hoàn qua cổng quản trị merchant VNPay, rồi Admin xác nhận kèm bằng chứng. Đây là quy trình thật (BR-RF-10), không phải mô phỏng.

> Chế độ `Demo` cũ đã bị **gỡ**. `Refunds:Mode=Demo` (hoặc số `0`) làm API **không khởi động** (fail-fast) thay vì âm thầm đổi hành vi. Đổi sang `Manual`.

## Cấu hình
`Refunds:Mode` (env `Refunds__Mode`, docker-compose `REFUND_MODE`):

| Giá trị | Hành vi |
|---|---|
| `Manual` (**mặc định**, kể cả khi không cấu hình) | Refund mới dùng `ManualRefundGateway`, không bao giờ gọi API Refund của VNPay. Admin xác nhận qua `POST /api/v1/refunds/confirm-manual`. Email hoàn tiền là email thật (nêu khoản hoàn về trong khoảng 2–3 ngày làm việc). |
| `Gateway` | Ưu tiên gateway tự động VNPAY. Chỉ bật khi merchant đã được VNPay cấp quyền Refund. |

`Manual` mà không có gateway thủ công đăng ký ⇒ `InvalidOperationException` (fail-closed), không refund nào được tạo, không gateway nào được gọi. Giá trị ngoài `Manual`/`Gateway` làm ứng dụng không khởi động.

## Luồng hoàn theo chuyến
Chuyến phải đang `Cancelled` hoặc `Terminated` (BR-RF-01/02); chuyến ở trạng thái khác trả **409**.

1. `GET /api/v1/refunds/candidates?tripId=&page=&pageSize=` (AdminOnly, chỉ đọc, `pageSize` 1–100): các Booking của chuyến có vé đủ điều kiện (`RefundEligibilityEvaluator`, BR-RF-01..09) và chưa nằm trong refund Requested/Processing/Succeeded. Mỗi mục có người mua, số tiền (tổng paid allocation của các vé đủ điều kiện, BR-PAY-06), mã vé, lý do. Vé NoShow và booking đã phục vụ đến bến xuống bị loại; đơn khứ hồi chỉ hiện phần của chuyến đó (BR-RF-08).
2. `POST /api/v1/refunds/batch-request` body `{ tripId, reason? }` (AdminOnly): tạo refund `Processing` (gateway `Manual`) cho mọi candidate bằng đúng `IRefundRequestService` (cùng chống lặp `REFUND_{bookingId}`, giới hạn số tiền còn hoàn, khóa giao dịch thương mại). Vé và Booking được chuyển sang `Cancelled`/`Terminated` tại bước này. Booking lỗi nghiệp vụ nằm ở `skippedItems` (kèm lý do), không làm hỏng cả lô. Gọi lại không hoàn trùng.
3. Nhân viên hoàn tiền qua cổng quản trị merchant VNPay theo `vnp_TxnRef` / mã giao dịch.
4. `POST /api/v1/refunds/confirm-manual` (AdminOnly): bắt buộc bằng chứng + đúng số tiền, một lần duy nhất → `Succeeded`, gửi email hoàn tiền cho người mua.

Yêu cầu hoàn lặp lại cho cùng booking trả đúng refund cũ; sau khi `Succeeded`, `requiresManualAction = false`.

Tiếp theo (chưa làm trong PR này): xuất CSV danh sách cần hoàn, xác nhận hàng loạt, giao diện Admin.

## Vai trò
Chỉ `Admin` (không có role nhân viên hoàn tiền riêng; `Staff` là nhân viên soát vé). Cùng một Admin tạo yêu cầu và xác nhận.

## Dữ liệu cũ
- `FinancialTransaction.IsDemoRefund` giữ lại làm **lịch sử**: refund đã tạo khi Demo còn tồn tại vẫn mang `true`, email/phản hồi của chúng vẫn có nhãn DEMO để không bị hiểu nhầm là tiền thật. Code mới không bao giờ đặt `true`; không có migration phá dữ liệu.
- Refund `VNPAY` đang `Processing` (mơ hồ, chưa có `GatewayReference`) **không bao giờ bị tự giải quyết**: `confirm-manual` vẫn từ chối (409), `request` trả lại đúng refund cũ, không gửi thêm gì tới VNPay. Với `Manual` tình huống này không phát sinh mới vì không có lệnh gọi sang VNPay (xem `docs/C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md`, không còn áp dụng).
- `POST /api/v1/refunds/{id}/reconcile` (QueryDr, chỉ đọc) không đổi.

## Độ lệch so với BR v2.0
BR-RF-10 yêu cầu chỉ ghi `Succeeded` sau khi xác minh khoản hoàn đã thực sự chuyển đến người nhận. Ở `Manual`, bước xác minh là việc của Admin qua bằng chứng đối soát bắt buộc; hệ thống kiểm tra đúng số tiền và chặn xác nhận lần hai, nhưng **không tự kiểm chứng với VNPay**.
