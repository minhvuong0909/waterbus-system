# Lỗi tích hợp còn mở

Gom từ các review PR #13/#14/#17 ngày 16/10/2026 để tránh nhiều báo cáo tạm trùng nhau. PR đã merge không đồng nghĩa các tình huống dưới đây đã được sửa.

| Ưu tiên | Phần | Vấn đề cần xử lý | Chủ trì |
|---|---|---|---|
| P1 | Payment #13 | Callback thành công sau callback thất bại cùng mã bị idempotency bỏ qua, không đánh dấu đối soát. Xem PaymentConfirmationService dòng kiểm IdempotencyKey; BR-PAY-01/05. | Nhật Anh |
| P1 | Refund #13 | Timeout chưa có GatewayReference có thể kẹt Processing: reconcile bắt buộc reference đã biết, còn manual bị chặn. Cần đường đối soát đủ mạnh, không tự chuyển Succeeded chỉ theo số tiền. BR-RF-07/10. | Nhật Anh |
| P1 | Scanner #14 | qrVerifier nhận chữ ký RSA chỉ vì dài >64 và có bypass mock_direct_id; phải verify RSA thực sự. | Tuấn |
| P1 | Scanner #14 | syncService khi gửi lỗi vẫn markEventsSynced; phải giữ Pending đến khi nhận ACK, xử lý conflict riêng. | Tuấn |
| P1 | Scanner #14 | TripStopCall ID tự tạo, thiếu kiểm cửa sổ/phân công; cần API preload/batch sync và ID thật. | Vương + Tuấn |
| P1 | Web checkout | createBooking/confirmPayment vẫn có mock trong đường không bật mock; cần dùng OrderId, X-Guest-Token, IPN xác nhận, vé/QR backend. | Viễn + Nhật Anh |
| P2 | Realtime #17 | Sau hết automatic reconnect, start retry loop không gọi reload snapshot; radar/alerts có thể báo Live với dữ liệu cũ. | Kiệt |
| P2 | Seat sync #17 | Bỏ đoạn trong event, gán trạng thái toàn ghế; cần overlap theo VisitOrder. Không có freeIds thì rơi sang mock và không nghe nhả ghế. | Kiệt + Viễn |
| P2 | Search | SearchTrips vẫn dùng Route endpoints/Station.OrderIndex, giá 0 placeholder và count không theo đoạn. Dùng trip-detail/seat-map làm nguồn chuẩn cho màn chọn ghế, rồi chỉnh search riêng. | Vương |
| P2 | Deploy/CI | Compose cần truyền khóa QR và các cấu hình mới; CI còn lọc 4 test booking cũ và chưa test mobile. | Kiệt |

## Bằng chứng trước khi gom tài liệu

- PR #13 commit b77aa92: toàn bộ test hiện có với SQL/ba Redis thật: **450 pass, 0 fail, 0 skip**. Hai regression test bổ sung trong snapshot review tái hiện lỗi payment/refund; chưa được đưa vào suite của PR.
- PR #17 commit fe842e7: build production/mock pass; harness dùng source thật + fake transport cho 2 control checks pass, 3 regression checks fail (snapshot recovery, segment và empty freeIds).
- Chưa xác nhận E2E VNPay sandbox hoặc check-in từ điện thoại thật. Đây là danh sách công việc; không suy CI xanh thành đã nghiệm thu toàn flow.
