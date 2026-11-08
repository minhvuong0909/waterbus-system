# Đề xuất quyết định nghiệp vụ C3 — Refund VNPay timeout (`Processing`, chưa có `GatewayReference`)

Trạng thái: **ĐỀ XUẤT, chờ phê duyệt.** Chưa thay đổi bất kỳ chuyển trạng thái tài chính nào trong code. Hướng đã chọn: *đối soát thủ công có kiểm soát, dựa trên bằng chứng*; không tự đổi trạng thái, không gửi lại mù quáng. Căn cứ BR: BR-PAY-03 (không refund trùng khi timeout), BR-PAY-06, BR-RF-07, BR-RF-10.

## 1. Khả năng thật của VNPay (đặc tả chính thức, đọc ngày 2026-11-09)
Nguồn: `sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr&refund.html` (đã đọc trực tiếp).

| Điểm | Đặc tả |
|---|---|
| `querydr` — tham số gửi | `vnp_RequestId` (của lần truy vấn), `vnp_TxnRef` (mã **thanh toán gốc**), `vnp_TransactionDate`, `vnp_OrderInfo`, tuỳ chọn `vnp_TransactionNo` ("mã giao dịch ghi nhận tại VNPAY") |
| `querydr` — kết quả | MỘT bản ghi: `vnp_TxnRef`, `vnp_Amount`, `vnp_TransactionNo`, `vnp_TransactionType` (01 thanh toán / 02 hoàn toàn phần / 03 hoàn một phần), `vnp_TransactionStatus` (00, 01, 02, 04, 05 đang xử lý hoàn, 06 đã gửi NH, 07, 09 hoàn bị từ chối) |
| `vnp_RequestId` của refund trong kết quả `querydr` | **Không được trả lại.** Không có cách tra theo `RequestId` của refund |
| Nhiều refund một phần trên cùng `vnp_TxnRef` | **Không có tài liệu** về cách phân biệt trong `querydr` (chỉ một bản ghi trả về) |
| `refund` — kết quả | `vnp_TransactionNo` = mã giao dịch hoàn tại VNPay **chỉ có trong response**; response bị mất khi timeout |
| `refund` ResponseCode | 00 thành công; **94 "đã gửi yêu cầu hoàn trước đó, VNPAY đang xử lý"**; 95 giao dịch gốc không thành công; 91 không thấy giao dịch; 97 checksum; 99 lỗi khác |
| Gửi lại `refund` cùng `vnp_RequestId` | **Không có tài liệu** về việc có trả lại kết quả cũ hay không ("không trùng lặp trong ngày" chỉ là yêu cầu với merchant) |
| Tra cứu ngoài hệ thống (cổng quản trị merchant, file đối soát) | **Không có tài liệu** trong trang này |

Hệ quả: **không thể tự động tương quan một refund bị timeout với một giao dịch hoàn cụ thể**, và **không thể chứng minh "chưa hoàn" bằng API** (querydr chỉ trả một bản ghi). Do đó mọi kết luận phải dựa trên bằng chứng ngoài hệ thống do Admin cung cấp, được máy kiểm tra thêm.

## 2. Bằng chứng có thẩm quyền để xác định đúng MỘT refund
Một refund đang `Processing` (VNPAY, `GatewayReference` null) chỉ được xác định là "giao dịch hoàn X của VNPay" khi có **đủ**:
1. **Mã giao dịch hoàn VNPay (`vnp_TransactionNo` của refund)** do Admin lấy từ cổng quản trị merchant/đối soát VNPay/phiếu hỗ trợ VNPay, nơi họ đối chiếu bằng `vnp_RequestId` (= `refund.Id` dạng N, hệ thống đã gửi), thời điểm, số tiền và `vnp_TxnRef` gốc.
2. **Xác minh máy** bằng `querydr` có truyền `vnp_TransactionNo` = mã trên: phản hồi hợp lệ chữ ký, `TransactionType` 02/03, `TransactionStatus` 00, `vnp_TxnRef` == payment gốc, `vnp_Amount` == `refund.Amount × 100`.
3. **Tính duy nhất**: mã đó chưa gắn với refund nào khác (chỉ mục unique có lọc trên `(GatewayName, GatewayReference)` cho Refund), và tổng hoàn vẫn ≤ số tiền thu được (BR-PAY-06).
4. Bằng chứng văn bản (mã tham chiếu phiếu/ảnh chụp) lưu kèm.

Không bao giờ suy ra thành công từ số tiền một mình (đúng với test hiện có `RefundTimeoutSafetyTests`).

## 3. Ghi nhận refund đã thành công (không gửi thêm request tới gateway)
Endpoint đề xuất: `POST /api/v1/refunds/{id}/resolve-timeout` (chỉ Admin) với `outcome = VerifiedSucceeded`, `gatewayRefundTransactionNo`, `evidence`.
- Chạy trong `ICommercialTransactionRunner` + khoá dòng (`GetRefundForUpdateAsync`); điều kiện: refund `Processing`, `GatewayName == VNPAY`, `GatewayReference` null.
- Làm đủ mục 2; đạt → `Succeeded`, `GatewayReference`, `CompletedAt`, `ManualProcessedByAdminId`, `ManualRefundEvidence`; enqueue email refund (outbox) như đường hiện có.
- Idempotent: gọi lại cùng `(refundId, TransactionNo)` trả kết quả cũ; khác mã → 409.
- **Không gọi `RefundAsync` của gateway.** Mọi lệnh gọi tới VNPay trong endpoint chỉ là `querydr` (chỉ đọc).

## 4. Refund chắc chắn KHÔNG thành công — retry an toàn
Vì không thể chứng minh "chưa hoàn" bằng API, đề xuất:
- Chỉ chuyển `Processing → Requested` (đã có nghĩa "cần hành động thủ công") khi **đồng thời**: (a) Admin đính kèm xác nhận ngoài hệ thống từ VNPay rằng không có giao dịch hoàn cho `RequestId`/`TxnRef`/số tiền đó (hoặc VNPay xác nhận bị từ chối); (b) đã qua thời gian chờ tối thiểu `T` kể từ `ProcessedAt` (đề xuất 24 giờ, vì `RequestId` chỉ cần duy nhất trong ngày và mã 94 cho thấy VNPay có thể còn xử lý); (c) `querydr` hiện không trả bản ghi 02/03 thành công/đang xử lý (05/06) cho payment gốc.
- Sau `Requested`, **mặc định hoàn thủ công qua đường BR-RF-10 hiện có** (không gửi lại gateway). Gửi lại tự động qua gateway là giai đoạn sau, cần `RequestId` mới theo từng lần thử (thêm bộ đếm lần thử) và vẫn phải qua cùng các kiểm tra; không làm trong lần này.

## 5. Kết quả mơ hồ — vẫn bị chặn trùng
Mơ hồ = thiếu mã giao dịch hoàn; `querydr` trả bản ghi không khớp; nhiều refund một phần trên cùng payment; chữ ký sai; VNPay trả 94/05/06. Khi đó refund **giữ `Processing`**: không xác nhận thủ công, không gửi lại, không tạo refund mới cho cùng Booking (`GetActiveRefundForBooking`) và số tiền vẫn tính vào tổng đã cam kết (BR-PAY-06). Chỉ thoát bằng bằng chứng mục 2 hoặc mục 4. Danh sách `GET /refunds/reconciliation-required` (đã có) hiển thị tuổi để theo dõi.

## 6. Kiểm soát bắt buộc
- Idempotency: `RequestId` refund ổn định (đã có); resolve idempotent theo `(refundId, TransactionNo)`.
- Khoá: `ICommercialTransactionRunner` (applock) + `GetRefundForUpdateAsync`; so sánh chữ ký `CryptographicOperations.FixedTimeEquals` (đã có trong `VnPayService`).
- Phân quyền: `AdminOnly`; **cần quyết định** có tách vai trò tài chính hoặc yêu cầu người duyệt thứ hai (4-eyes).
- Audit: ai/lúc nào/bằng chứng/outcome lưu trên refund (`ManualProcessedByAdminId`, `ManualRefundEvidence`); đề xuất thêm bảng sự kiện đối soát bất biến (cần migration).
- DB: chỉ mục unique có lọc cho `GatewayReference` của refund.

## 7. Test hồi quy trên SQL Server thật (sẽ viết sau khi duyệt)
1. Timeout → refund `Processing`, `GatewayReference` null, gateway fake `Calls == 1`.
2. Resolve thành công đúng bằng chứng → `Succeeded`, outbox email 1 lần, gateway refund **không** gọi lại.
3. Sai số tiền / sai type / sai `TxnRef` / chữ ký sai / mã trùng với refund khác → từ chối, trạng thái giữ nguyên.
4. Hai Admin resolve đồng thời → đúng một thắng; gọi lại cùng mã idempotent.
5. Nhiều refund một phần cùng payment: không khớp được mã → vẫn chặn.
6. Chuyển `Requested`: thiếu xác nhận VNPay, hoặc chưa đủ `T`, hoặc `querydr` thấy 02/03 → từ chối; đủ điều kiện → `Requested`, sau đó chỉ manual (BR-RF-10), không gửi gateway.
7. Passenger/Guest/Staff/Captain → 403; không token → 401.
8. Trong lúc `Processing`: yêu cầu hoàn lại trả đúng refund cũ; tổng hoàn không vượt payment.

## 8. Quyết định cần từ nghiệp vụ
1. Vai trò được phép resolve + có cần người duyệt thứ hai?
2. Thời gian chờ tối thiểu `T` (đề xuất 24 giờ)?
3. Sau `Requested`: chỉ hoàn thủ công (đề xuất) hay cho phép gửi lại qua gateway?
4. Chấp nhận loại bằng chứng nào (ảnh chụp cổng quản trị, email/phiếu VNPay, file đối soát)?
5. SLA/cảnh báo cho refund `Processing` quá hạn.
