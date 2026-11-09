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

## Điều KHÔNG thay đổi
- Refund `VNPAY` đang `Processing` (mơ hồ, chưa có `GatewayReference`) **không bao giờ bị tự giải quyết** bởi chế độ này: `confirm-manual` vẫn từ chối (409), `request` trả lại đúng refund cũ, không gửi thêm gì tới VNPay. Hướng giải quyết thật nằm ở `docs/C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md` (công việc production tương lai, **chưa triển khai**).
- `POST /api/v1/refunds/{id}/reconcile` (QueryDr, chỉ đọc) không đổi.
- Refund không được đánh dấu lại demo/không-demo sau khi tạo; đổi `Refunds:Mode` không ảnh hưởng refund cũ.

## Độ lệch so với BR v2.0 (ghi nhận, không tự suy diễn)
BR-RF-10 yêu cầu chỉ ghi `Succeeded` sau khi xác minh tiền thực sự đến người nhận. Ở chế độ Demo không có tiền thật nên `Succeeded` là mô phỏng, được đánh dấu tường minh (`IsDemoRefund`, `isDemo`, `notice`, email). **Không dùng Demo cho vận hành tiền thật**; chuyển sang `Gateway` (hoặc quy trình thật của C3) khi có quyền Refund.

## Migration
`AddRefundDemoFlag`: thêm cột `FinancialTransactions.IsDemoRefund bit NOT NULL DEFAULT 0` (dữ liệu cũ = không demo). Không phá dữ liệu.
