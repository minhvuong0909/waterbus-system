# Lỗi tích hợp còn mở

Gom từ các review PR #13/#14/#17 ngày 16/10/2026 để tránh nhiều báo cáo tạm trùng nhau. PR đã merge không đồng nghĩa các tình huống dưới đây đã được sửa.

| Ưu tiên | Phần | Vấn đề cần xử lý | Chủ trì |
|---|---|---|---|
| P1 | Payment #13 | Callback thành công sau callback thất bại cùng mã bị idempotency bỏ qua, không đánh dấu đối soát. Xem PaymentConfirmationService dòng kiểm IdempotencyKey; BR-PAY-01/05. | Nhật Anh |
| P1 | Refund #13 | Timeout chưa có GatewayReference có thể kẹt Processing: reconcile bắt buộc reference đã biết, còn manual bị chặn. Cần đường đối soát đủ mạnh, không tự chuyển Succeeded chỉ theo số tiền. BR-RF-07/10. | Nhật Anh |
| P1 | Scanner #14 | qrVerifier nhận chữ ký RSA chỉ vì dài >64 và có bypass mock_direct_id; phải verify RSA thực sự. | Tuấn |
| P1 | Scanner #14 | syncService khi gửi lỗi vẫn markEventsSynced; phải giữ Pending đến khi nhận ACK, xử lý conflict riêng. | Tuấn |
| P1 | Scanner #14 | TripStopCall ID tự tạo, thiếu kiểm cửa sổ/phân công; cần API preload/batch sync và ID thật. | Vương + Tuấn |
| P0 | Payment config | `VnPayService.RequireSecret/RequireTmnCode` chỉ chặn `null`; compose truyền `""` khi `.env` để trống nên HMAC dùng key rỗng và **IPN giả tự ký được xác nhận đơn** (đã tái hiện 22/10/2026). Phải coi chuỗi rỗng là thiếu cấu hình (fail-fast). | Người tích hợp VNPay |
| P1 | Payment retry | Một IPN thất bại huỷ cả Order và nhả ghế, nên "Thanh toán lại" không còn tác dụng; tạo lại URL dùng lại đúng `vnp_TxnRef` của attempt `Requested`, VNPay có thể từ chối TxnRef trùng. Đối chiếu BR-PAY-03 (mỗi lần thử là một transaction). | Người tích hợp VNPay |
| P1 | Payment late | Tiền về sau khi Order hết hạn được ghi `ReconciliationRequired` nhưng chưa có màn Admin/luồng hoàn tiền cho khoản này; web đã báo khách "đã nhận tiền, sẽ hoàn". | Nhật Anh + Admin UI |
| P2 | Payment IP | `vnp_IpAddr` đang là `::ffff:172.18.0.1` (IPv6-mapped, IP nội bộ Docker); nên `MapToIPv4()` và lấy IP thật sau nginx. | Người tích hợp VNPay |
| P2 | Demo data | Seed chỉ có 1 chuyến (ngày chạy seed lần đầu), không có tuyến `Sightseeing` hay chiều ngược; chưa có API tạo/sinh chuyến. SearchTrips vẫn trả chuyến `salesState != open`. | Vương |
| P2 | Realtime #17 | Sau hết automatic reconnect, start retry loop không gọi reload snapshot; radar/alerts có thể báo Live với dữ liệu cũ. | Kiệt |
| P2 | Seat sync #17 | Bỏ đoạn trong event, gán trạng thái toàn ghế; cần overlap theo VisitOrder. Không có freeIds thì rơi sang mock và không nghe nhả ghế. | Kiệt + Viễn |
| P2 | Search | SearchTrips vẫn dùng Route endpoints/Station.OrderIndex, giá 0 placeholder và count không theo đoạn. Dùng trip-detail/seat-map làm nguồn chuẩn cho màn chọn ghế, rồi chỉnh search riêng. | Vương |
| P2 | Deploy/CI | Compose cần truyền khóa QR và các cấu hình mới; CI còn lọc 4 test booking cũ và chưa test mobile. | Kiệt |

## Bằng chứng trước khi gom tài liệu

- PR #13 commit b77aa92: toàn bộ test hiện có với SQL/ba Redis thật: **450 pass, 0 fail, 0 skip**. Hai regression test bổ sung trong snapshot review tái hiện lỗi payment/refund; chưa được đưa vào suite của PR.
- PR #17 commit fe842e7: build production/mock pass; harness dùng source thật + fake transport cho 2 control checks pass, 3 regression checks fail (snapshot recovery, segment và empty freeIds).
- 22/10/2026, web + API qua Docker, VNPay giả lập bằng secret test và IPN tự ký: thành công → Ticket + QR, huỷ (24) → Order Expired + nhả ghế, chữ ký sai 97, sai tiền 04, replay 02, IPN không tới → timeout → reconcile (querydr 403 với TMN giả, xử lý êm), tiền về sau hết hạn → màn "đã nhận tiền", khứ hồi 1 payment phân bổ 2 Booking. Web gia hạn đồng hồ giữ ghế theo `expiresAt` của `create-vnpay-url`. Để VNPay gọi được IPN khi chạy local cần URL public (ngrok…) trỏ tới `GET /api/v1/payments/vnpay-ipn`.
- Chưa xác nhận E2E VNPay sandbox hoặc check-in từ điện thoại thật. Đây là danh sách công việc; không suy CI xanh thành đã nghiệm thu toàn flow.
