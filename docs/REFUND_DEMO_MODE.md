# Chế độ hoàn tiền Demo / Gateway (Sandbox & MVP)

Merchant VNPay hiện chỉ có sandbox cho thanh toán/IPN và **chưa được xác minh quyền Refund**. Hệ thống không cố vượt giới hạn đó:
chế độ mặc định không bao giờ gọi API Refund của VNPay.

## Cấu hình
`Refunds:Mode` (env `Refunds__Mode`, docker-compose `REFUND_MODE`):

| Giá trị | Hành vi |
|---|---|
| `Demo` (**mặc định**, kể cả khi không cấu hình) | Refund mới dùng `ManualRefundGateway` (không gọi VNPay). `FinancialTransaction.IsDemoRefund = true`. Admin xác nhận qua `POST /api/v1/refunds/confirm-manual`. |
| `Gateway` | Hành vi trước đây (ưu tiên gateway tự động, VNPAY). Chỉ bật khi merchant đã được VNPay cấp quyền Refund. `IsDemoRefund = false`. |

`Demo` mà không có gateway thủ công đăng ký ⇒ `InvalidOperationException` (fail-closed), không refund nào được tạo, không gateway nào được gọi.

## Luồng (Demo)
1. `POST /api/v1/refunds/request` (AdminOnly): dùng nguyên quy tắc điều kiện (`RefundEligibilityEvaluator`), phân bổ theo vé (`RefundTicketAllocation`, số tiền snapshot), khoá giao dịch thương mại và idempotency như cũ → refund `Processing`, `gateway = "Manual"`, `isDemo = true`, `requiresManualAction = true`, `notice = "DEMO: …"`.
2. `POST /api/v1/refunds/confirm-manual` (AdminOnly): bắt buộc bằng chứng + đúng số tiền, một lần duy nhất → `Succeeded`; phản hồi có `isDemo = true` và `notice`.
3. Email hoàn tiền (outbox) cho refund demo có tiền tố `[DEMO]`, nêu rõ *không có tiền thật được chuyển* và bỏ câu về thời gian tiền về tài khoản.

`Succeeded` của refund demo nghĩa là **Admin ghi nhận đã mô phỏng**, không phải tiền đã chuyển.

Yêu cầu hoàn lặp lại cho cùng booking trả đúng refund cũ; sau khi đã `Succeeded`, `requiresManualAction = false` (chỉ `Requested` hoặc `Processing` ở gateway Manual mới còn việc thủ công), nên giao diện không hiện lại form xác nhận.

## An toàn môi trường (Demo KHÔNG dành cho tiền thật)
Refund demo chỉ **mô phỏng** việc hoàn tiền; `Succeeded` không có nghĩa tiền đã về khách. Tuyệt đối không dùng Demo cho thanh toán tiền thật.
- **Guard fail-closed lúc khởi động:** nếu `Refunds:Mode=Demo` mà `VnPay:BaseUrl` hoặc `VnPay:WebApiUrl` trỏ tới VNPay **không phải sandbox** (host `vnpay.vn`/`vnpayment.vn` không bắt đầu bằng `sandbox.`, vd `pay.vnpay.vn`), ứng dụng **không khởi động** (`OptionsValidationException`). Chuyển sang `Refunds:Mode=Gateway` (khi đã có quyền Refund) hoặc dùng sandbox.
- **Không phụ thuộc tên môi trường** và không cần cờ bổ sung: sandbox VNPay, localhost/mock và cấu hình để trống đều khởi động bình thường nên demo học thuật không bị ảnh hưởng.
- `Refunds:Mode` không hợp lệ (vd `2`, `Banana`) cũng làm ứng dụng không khởi động; không bao giờ âm thầm rơi vào hoàn tiền thật.
- **Giới hạn đã biết:** guard dựa trên host cấu hình, không phát hiện được merchant dùng host tự dựng/proxy trỏ tới VNPay production. Quy trình triển khai production vẫn phải đặt `Refunds__Mode=Gateway` hoặc triển khai C3. Refund tạo ở Demo vẫn là `IsDemoRefund = true` vĩnh viễn kể cả khi đổi chế độ.

## Điều KHÔNG thay đổi
- Refund `VNPAY` đang `Processing` (mơ hồ, chưa có `GatewayReference`) **không bao giờ bị tự giải quyết** bởi chế độ này: `confirm-manual` vẫn từ chối (409), `request` trả lại đúng refund cũ, không gửi thêm gì tới VNPay. Hướng giải quyết thật nằm ở `docs/C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md` (công việc production tương lai, **chưa triển khai**).
- `POST /api/v1/refunds/{id}/reconcile` (QueryDr, chỉ đọc) không đổi.
- Refund không được đánh dấu lại demo/không-demo sau khi tạo; đổi `Refunds:Mode` không ảnh hưởng refund cũ.

## Độ lệch so với BR v2.0 (ghi nhận, không tự suy diễn)
BR-RF-10 yêu cầu chỉ ghi `Succeeded` sau khi xác minh tiền thực sự đến người nhận. Ở chế độ Demo không có tiền thật nên `Succeeded` là mô phỏng, được đánh dấu tường minh (`IsDemoRefund`, `isDemo`, `notice`, email). **Không dùng Demo cho vận hành tiền thật**; chuyển sang `Gateway` (hoặc quy trình thật của C3) khi có quyền Refund.

## Migration
`AddRefundDemoFlag`: thêm cột `FinancialTransactions.IsDemoRefund bit NOT NULL DEFAULT 0` (dữ liệu cũ = không demo). Không phá dữ liệu.
