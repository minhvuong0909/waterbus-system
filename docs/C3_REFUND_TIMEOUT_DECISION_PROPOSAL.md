# Đề xuất quyết định nghiệp vụ C3 — Refund VNPay timeout (`Processing`, chưa có `GatewayReference`) — BẢN 3

> **KHÔNG CÒN ÁP DỤNG (10/11/2026).** Nhóm đã chốt với thầy hướng khác: hệ thống không tự động hoàn tiền và không gọi API Refund của VNPay; Admin tạo yêu cầu, nhân viên hoàn qua cổng merchant, Admin xác nhận kèm bằng chứng (`Refunds:Mode=Manual`, xem `docs/REFUND_MANUAL_MODE.md`). Vì không có lệnh hoàn gửi sang VNPay nên tình huống timeout không phát sinh mới. Tài liệu giữ lại để tham khảo nếu sau này bật `Gateway` sau khi VNPay cấp quyền Refund.

Trạng thái: **ĐỀ XUẤT BẢN 3 (sau review v2: 09 không đủ cho B; B nguyên tử + chặn tái nhập), chờ phê duyệt tường minh.** Chưa thay đổi bất kỳ chuyển trạng thái tài chính nào trong code; không triển khai hỗ trợ gateway mang tính suy đoán. Hướng đã duyệt: *đối soát thủ công có kiểm soát, dựa trên bằng chứng*. Căn cứ BR: BR-PAY-03, BR-PAY-06, BR-RF-07, BR-RF-10.

Thay đổi so với bản 1: (1) không còn tuyên bố `querydr` tra được theo mã giao dịch hoàn — **chưa được xác minh**; (2) 24 giờ chỉ là ngưỡng *rà soát vận hành*, không là bằng chứng; (3) tách 3 kết cục độc lập; (4) quy tắc tương quan bằng chứng; (5) audit bất biến, 2 người duyệt, ràng buộc DB; (6) test xác định.

## 1. Khả năng thật của VNPay — đã xác minh vs chưa xác minh
Nguồn duy nhất đã đọc: trang đặc tả sandbox `querydr&refund` (đọc lại ngày 2026-11-09 để trích đúng chữ). Không gọi sandbox, không có xác nhận từ VNPay.

| # | Điều khẳng định | Trạng thái | Căn cứ |
|---|---|---|---|
| V1 | `querydr` nhận `vnp_TxnRef` của giao dịch **thanh toán gốc** và `vnp_TransactionDate` = thời điểm tạo thanh toán (`vnp_CreateDate` của lệnh `pay`) | **Đã xác minh (đặc tả)** | bảng tham số request |
| V2 | `vnp_TransactionNo` trong request `querydr` là tuỳ chọn, mô tả "mã giao dịch ghi nhận tại hệ thống VNPAY", và phần giải thích trỏ tới `vnp_TransactionNo` **của luồng thanh toán PAY** | **Đã xác minh (đặc tả)** | ghi chú theo luồng PAY/Token/Trả góp/Định kỳ |
| V3 | **Chuỗi checksum request của `querydr` KHÔNG chứa `vnp_TransactionNo`** (chỉ RequestId, Version, Command, TmnCode, TxnRef, TransactionDate, CreateDate, IpAddr, OrderInfo) | **Đã xác minh (đặc tả)** | quy tắc tạo checksum |
| V4 | Response `querydr` là MỘT bản ghi (`TxnRef`, `Amount`, `TransactionNo`, `TransactionType` 01/02/03, `TransactionStatus`), **không echo `vnp_RequestId`** của refund | **Đã xác minh (đặc tả)** | bảng response |
| V5 | `refund` trả `vnp_TransactionNo` (mã giao dịch hoàn) **chỉ trong response**; mã 94 = đã nhận yêu cầu hoàn trước đó, đang xử lý | **Đã xác minh (đặc tả)** | bảng response/mã lỗi |
| **U1** | `querydr` + `vnp_TransactionNo` = mã giao dịch **hoàn** sẽ trả đúng bản ghi hoàn đó | **CHƯA XÁC MINH.** V2+V3 gợi ý ngược lại (tham số không nằm trong checksum, mô tả thuộc về giao dịch thanh toán); không thể coi là cơ chế tra cứu hoàn | cần thí nghiệm sandbox hoặc xác nhận VNPay |
| U2 | Khi có nhiều refund một phần trên cùng `TxnRef`, `querydr` trả bản ghi nào | **CHƯA XÁC MINH** (đặc tả im lặng) | như trên |
| U3 | Gửi lại `refund` cùng `vnp_RequestId` trả kết quả cũ / 94 / tạo mới | **CHƯA XÁC MINH** | như trên |
| U4 | Cổng quản trị merchant / file đối soát có tra được theo `RequestId` hoặc theo (TxnRef, số tiền, thời điểm) | **CHƯA XÁC MINH** | như trên |

**Quyết định thiết kế vì U1–U4 chưa rõ:** `querydr` **không** được dùng làm bằng chứng xác định cho một refund cụ thể trong bản này. Nó chỉ là kiểm tra *tương thích, chỉ đọc* (xem 3.2). Bằng chứng có thẩm quyền phải đến từ kênh VNPay mà Admin xuất trình (xác nhận bằng văn bản/đối soát có mã giao dịch hoàn), và hệ thống kiểm tra tính nhất quán chứ không "đoán".

### 1.1 Thí nghiệm sandbox đề xuất (chưa chạy — cần merchant sandbox của nhóm và duyệt chạy)
Mục đích: biến U1–U3 thành sự thật đã biết. Mỗi bước ghi lại request/response (che secret) vào `docs/` làm bằng chứng.
1. Tạo 1 thanh toán sandbox thành công (`TxnRef=T1`, 100.000đ). Gọi `refund` một phần 30.000đ (RequestId=R1) → ghi `TransactionNo=N1`. Gọi tiếp `refund` một phần 20.000đ (R2) → `N2`.
2. U1: `querydr(TxnRef=T1, TransactionNo=N1)`, rồi `(T1, N2)`, rồi `(T1, mã không tồn tại)`. Quan sát `vnp_TransactionNo`/`Amount`/`TransactionType` trả về có đổi theo tham số không.
3. U2: `querydr(T1)` không kèm `TransactionNo` → ghi bản ghi nào được trả.
4. U3: gửi lại `refund` với R1 (cùng mọi tham số) → ghi ResponseCode/TransactionNo; ghi cả trường hợp đổi số tiền.
5. Mô phỏng timeout bằng cách bỏ response (ngắt phía client sau khi gửi) rồi lặp bước 2–4.
Kết luận được phép rút ra: chỉ phần quan sát được trên sandbox, ghi rõ "sandbox ≠ production" (U1–U4 vẫn cần xác nhận production của VNPay trước khi vận hành tiền thật). Nếu U1 = đúng và ổn định → mở lại 3.2 thành bằng chứng máy xác định. Nếu không → chỉ dùng bằng chứng ngoài hệ thống.

## 2. Ba kết cục — tách bạch, mỗi kết cục có điều kiện riêng
Refund đối tượng: `FinancialTransaction` loại Refund, `GatewayName=VNPAY`, `Status=Processing`, `GatewayReference` null.

| Kết cục | Ý nghĩa | Hành động hệ thống cho phép | Gửi tiền/gateway? |
|---|---|---|---|
| **A. Verified-Succeeded** | VNPay xác nhận chính refund đó đã thực hiện | Ghi nhận **chính refund hiện có** → `Succeeded` | **Không** gọi `RefundAsync`, không tạo refund mới |
| **B. Verified-NotExecuted** | **Bằng chứng có thẩm quyền của VNPay gắn đúng `RequestId` của lệnh refund gốc** xác nhận lệnh đó không được thực hiện và sẽ không được thực hiện về sau | **Một giao dịch nguyên tử duy nhất** (mục 3.4): đóng refund cũ và tạo/giữ chỗ ngay refund thay thế thủ công đã được ủy quyền | Chỉ khi có phê duyệt thứ hai (mục 4.1); **chỉ hoàn thủ công, không bao giờ gửi lại qua gateway** |
| **C. Ambiguous** | mọi trường hợp còn lại | **Giữ `Processing`**; không retry, không payout, không tạo refund mới (`GetActiveRefundForBooking`), số tiền vẫn tính vào tổng đã cam kết (BR-PAY-06) | Không |

Quy tắc mặc định: **không đủ bằng chứng ⇒ C.** Trạng thái chỉ thoát C bằng A hoặc B qua đúng thủ tục dưới đây. Không có đường tắt theo thời gian.

### 2.1 Vai trò của 24 giờ (hoặc `T` do nghiệp vụ chọn)
`T` **không phải bằng chứng** rằng refund không xảy ra. Nó chỉ là:
- ngưỡng để refund `Processing` xuất hiện trong hàng đợi "cần rà soát" (`reconciliation-required` thêm trường `ageHours`, `overdue`);
- điều kiện cần *tối thiểu* (không đủ) để được mở yêu cầu kết cục B — tránh xử lý sớm khi VNPay còn đang xử lý (mã 94).
Qua `T` mà không có xác nhận VNPay ⇒ vẫn C. Hết hạn không bao giờ tự động đổi trạng thái hay cho phép payout.

## 3. Bằng chứng và tương quan (correlation)

### 3.1 Tập khoá tương quan bắt buộc
Hệ thống chỉ chấp nhận kết cục A/B khi bằng chứng **khớp đồng thời, từng trường** với bản ghi nội bộ:

| Trường bằng chứng | So với | Quy tắc |
|---|---|---|
| `vnp_RequestId` của lệnh hoàn (do VNPay xác nhận) | `refund.Id` dạng `N` đã gửi | bằng nhau tuyệt đối (so chuỗi cố định) |
| `vnp_TxnRef` | `TxnRef` của Payment gốc của booking | bằng nhau |
| `vnp_Amount` (đơn vị ×100) | `refund.Amount × 100` | bằng nhau; **không bao giờ** suy thành công chỉ từ số tiền |
| Tiền tệ | `VND` | bằng nhau (BR chỉ VND) |
| Ngày giao dịch hoàn (GMT+7) | cửa sổ `[refund.ProcessedAt − 5 phút, +T_max]` đổi sang GMT+7 | nằm trong cửa sổ; ngoài cửa sổ ⇒ C |
| `vnp_TransactionNo` (mã hoàn) cho A | chưa dùng cho refund nào khác | duy nhất (ràng buộc DB 5.3) |

Nếu **bất kỳ** trường nào thiếu hoặc không khớp, hoặc không thể chứng minh bằng chứng thuộc về *đúng refund này* (ví dụ nhiều refund một phần cùng TxnRef, cùng số tiền, gần thời điểm nhau) ⇒ **C, không hoàn tất**.

### 3.2 Nguồn bằng chứng, xếp hạng
1. **(Có thẩm quyền) Xác nhận/đối soát từ VNPay** xuất trình bởi Admin: tập tin/phiếu/email nêu đủ các trường 3.1. Lưu bản gốc (hash SHA-256 + tham chiếu), không lưu dữ liệu thẻ.
2. **(Kiểm tra chéo, không đủ một mình)** `querydr(TxnRef)` chỉ đọc, kiểm tra chữ ký (`FixedTimeEquals`), `Amount`, `TransactionType` 02/03. Chỉ dùng để **phủ quyết**: nếu `querydr` cho thấy trạng thái trái với bằng chứng (ví dụ kết cục B nhưng thấy giao dịch hoàn thành công/đang xử lý 05/06) ⇒ C. Kết quả `querydr` **không** được dùng để chứng minh A hoặc B cho tới khi U1/U2 được xác minh (mục 1.1).
3. Không chấp nhận: ảnh chụp không đọc được các trường, lời khai miệng, suy luận từ số dư.

### 3.3 Kết cục B (không thực hiện)
**`vnp_TransactionStatus=09` (hay bất kỳ trạng thái nào của `querydr`) KHÔNG BAO GIỜ độc lập đủ cho B**: `querydr` chưa được chứng minh nhận diện được một `RequestId` refund cụ thể (U1/U2), nên 09 có thể thuộc về một giao dịch hoàn khác trên cùng `TxnRef`. `querydr` chỉ được phép *phủ quyết* B, không được chứng minh B.

B chỉ hợp lệ khi có **đồng thời**:
- (a) **Bằng chứng có thẩm quyền do VNPay phát hành** (văn bản/đối soát/phiếu hỗ trợ có định danh VNPay), nêu **đúng `RequestId` của lệnh refund gốc** (= `refund.Id` dạng `N`), `TxnRef`, số tiền, ngày, và kết luận lệnh đó **không được thực hiện / bị từ chối cuối cùng** (không còn có thể thực thi muộn). Bằng chứng chỉ nêu `TxnRef`+số tiền mà không gắn `RequestId` ⇒ không hợp lệ cho B. Ngay cả khi U1 sau này được xác minh, 09 từ `querydr` vẫn chỉ là bằng chứng bổ trợ, không thay thế (a).
- (b) qua ngưỡng rà soát `T`;
- (c) `querydr` không phủ quyết (không thấy 02/03 thành công hoặc 05/06 cho giao dịch gốc);
- (d) tổng đã cam kết (kể cả refund thay thế) ≤ số đã thu (BR-PAY-06).
Thiếu bất kỳ ý nào ⇒ C.

### 3.4 B phải là một chuyển đổi nguyên tử, giữ nguyên rào chắn chống tái nhập
**Vấn đề đã xác nhận trong code hiện có** (`FinancialTransactionRepository`, `RefundRequestService.PrepareAsync`):
- `GetActiveRefundForBookingAsync`, `GetCommittedRefundTotalAsync`, `GetAllocatedRefundTicketIdsAsync` chỉ tính trạng thái `Requested/Processing/Succeeded` — **loại `Failed`**. Nếu chỉ đổi refund cũ sang `Failed`, ba rào chắn (refund đang hoạt động theo booking, tổng đã cam kết theo payment, vé đã được phân bổ hoàn) cùng biến mất.
- Khi đó `PrepareAsync` sẽ không thấy refund nào, đi tiếp, và **tạo một refund mới `Processing` rồi gọi gateway tự động** ⇒ nguy cơ chi tiền hai lần (nếu VNPay thực thi muộn lệnh cũ, hoặc nếu bằng chứng B sai).
- `IdempotencyKey` của refund thường là `REFUND_{bookingId:N}` (unique index): refund thay thế phải dùng khoá khác, và đường thường sẽ không được tự sinh lại khoá cũ.

**Thiết kế (một `ICommercialTransactionRunner` + khoá dòng, một `SaveChanges`):**
1. Trong khoá, kiểm tra lại: refund cũ `Processing`, `GatewayName=VNPAY`, `GatewayReference` null, hồ sơ `PendingApproval`, chưa có hồ sơ `Applied` khác cho refund/booking này.
2. Tạo **refund thay thế** `FinancialTransaction` loại Refund: `Status=Requested` (trạng thái *đã được cả ba rào chắn tính*), `GatewayName="Manual"`, cùng `OrderId/RefundBookingId/OriginalPaymentId/Amount/Currency`, `IdempotencyKey=REFUND_MANUAL_{caseId:N}`, `ReplacesRefundId = cũ`, `PayoutAuthorizationCaseId = caseId`. **Chuyển/sao chép các `RefundTicketAllocation`** sang refund thay thế (cùng vé, cùng tiền) *trước* khi refund cũ rời các trạng thái được tính; vé không bao giờ ở trạng thái "không ai đang giữ".
3. Đổi refund cũ `Processing → Failed`, `FailureReason="VerifiedNotExecuted:{caseId}"`, `SupersededByRefundId = thay thế`.
4. Hồ sơ → `Applied`, ghi `SuccessorRefundId`; ghi sự kiện audit `Applied`.
Tất cả hoặc không gì cả (rollback toàn bộ nếu lỗi, kể cả lỗi chèn khoá trùng). Không có điểm thời gian nào mà tổng đã cam kết hoặc tập vé phân bổ bị thiếu.

**Chặn tái nhập (ba lớp, độc lập):**
- *Dữ liệu:* vì refund thay thế ở `Requested`, `GetActiveRefundForBookingAsync` trả đúng refund đó ⇒ `PrepareAsync` đi nhánh `existing is not null` (không gọi gateway). `GetCommittedRefundTotalAsync` và `GetAllocatedRefundTicketIdsAsync` vẫn tính số tiền/vé này.
- *Logic:* thêm điều kiện tường minh (khi triển khai) — mọi refund có `PayoutAuthorizationCaseId`/`ReplacesRefundId` bị cấm gọi `IRefundGateway.RefundAsync`; đường thường gặp refund `GatewayName="Manual"` không gửi gateway. Tồn tại hồ sơ `Applied` với kết cục B cho booking/payment ⇒ đường thường **không bao giờ** tạo refund mới, kể cả khi mọi refund của booking đã `Failed`/`Succeeded` (kiểm tra hồ sơ chứ không chỉ kiểm tra trạng thái refund).
- *DB:* unique filtered index `(RefundBookingId) WHERE TransactionType=Refund AND Status IN (Requested,Processing,Succeeded)` chặn hai refund hoạt động cùng booking; unique `(SuccessorRefundId)` và `(ReplacesRefundId)` trên hồ sơ/refund (mỗi refund cũ chỉ có một thay thế). **Cần kiểm tra trước khi tạo index:** dữ liệu hiện có không có booking nào vi phạm, và hỗ trợ hoàn một phần nhiều lần (nếu BR cho phép) — nếu BR cho phép nhiều refund hoạt động trên một booking thì chỉ mục này không dùng, thay bằng ràng buộc theo `OriginalPaymentId`+vé (xem câu hỏi mục 9).

**Hoàn tất payout thủ công:** refund thay thế `Requested` chỉ thành `Succeeded` qua `ManualRefundService.ConfirmAsync` hiện có (đường BR-RF-10, idempotent), bởi người **không** phải người mở hồ sơ; một refund thay thế = một payout. Không có bản ghi payout thứ hai.

**Rủi ro thực thi muộn / trả tiền hai lần sau B:** sau khi `Applied`, nếu bất kỳ nguồn nào (IPN, `querydr`, đối soát) cho thấy lệnh gốc được VNPay thực thi, hồ sơ chuyển `Disputed`, refund thay thế bị khoá không cho xác nhận payout (nếu chưa), hoặc mở sự cố tài chính (nếu đã trả) — không tự động đảo tiền. Vì vậy bằng chứng 3.3(a) phải nêu rõ lệnh gốc "không còn có thể thực thi".

## 4. Quy trình & quyền hạn (không đổi code trước khi duyệt)
Endpoint đề xuất (mới, chỉ Admin): 
- `POST /api/v1/refunds/{id}/reconciliation-cases` — mở hồ sơ đối soát (`Outcome` = `VerifiedSucceeded` | `VerifiedNotExecuted`, trường bằng chứng 3.1, tham chiếu/hash tài liệu). Trạng thái hồ sơ `PendingApproval`. **Chưa** đổi refund.
- `POST /api/v1/refunds/reconciliation-cases/{caseId}/approve` — người duyệt thứ hai (≠ người mở) → áp dụng kết cục trong một giao dịch.
- `POST /api/v1/refunds/reconciliation-cases/{caseId}/reject` — từ chối, ghi lý do; refund giữ `Processing`.
Không có endpoint nào gọi `RefundAsync` của gateway.

### 4.1 Phân quyền & hai người
- **Mặc định thiết kế (đề xuất, chưa phải ủy quyền chi tiền):** hai Admin **khác nhau**; người duyệt phải có quyền `FinanceApprover`. `OpenedBy ≠ ApprovedBy` (ép ở service và kiểm tra lại trong khoá); người mở không cần `FinanceApprover`. Không đủ hai người phù hợp ⇒ hồ sơ ở `PendingApproval`.
- Với B: người xác nhận payout thủ công (`ManualRefundService`) ≠ người mở hồ sơ (đề xuất).
- Không kích hoạt tiền thật trước khi: thí nghiệm sandbox + xác nhận chính thức của VNPay (mục 1.1, 7).

## 5. Audit bất biến, idempotency, đồng thời, chống trùng

### 5.1 Bảng audit bất biến `RefundReconciliationEvents` (migration mới)
Chỉ INSERT: `Id`, `RefundId`, `CaseId`, `EventType` (Opened/Approved/Rejected/Applied/Superseded), `Outcome`, `ActorUserId`, `ActorRole`, `OccurredAt` (UTC), `EvidenceHashSha256`, `EvidenceReference`, `EvidenceFieldsJson` (các trường 3.1 đã nhập + kết quả so khớp), `QueryDrSnapshotJson` (nếu có), `IdempotencyKey`, `RequestFingerprint`.
Bất biến thực thi ở DB: `INSTEAD OF UPDATE/DELETE` trigger ném lỗi **hoặc** quyền DB chỉ INSERT (chọn một trước khi triển khai); entity không có đường sửa trong code; test xác nhận UPDATE/DELETE thất bại. Sửa sai = sự kiện `Superseded` mới, không sửa cũ. Không ghi payload thanh toán đầy đủ/PII (AGENTS.md).

### 5.2 Idempotency
- Mỗi lệnh mở/duyệt mang `Idempotency-Key`; unique `(RefundId, IdempotencyKey)`.
- Cùng key + cùng `RequestFingerprint` ⇒ trả kết quả cũ; cùng key khác nội dung ⇒ 409.
- Duyệt lặp một hồ sơ đã `Applied` ⇒ trả kết quả cũ, không đổi gì.
- Mỗi refund tối đa **một** hồ sơ `PendingApproval` (unique filtered index trên `(RefundId) WHERE State='PendingApproval'`).

### 5.3 Chống trùng tham chiếu
Unique filtered index trên `FinancialTransaction(GatewayName, GatewayReference)` cho Refund (`WHERE GatewayReference IS NOT NULL AND Type=Refund`). Trước khi tạo index: **bước kiểm tra dữ liệu hiện có** (query phát hiện trùng; migration dừng nếu có trùng — AGENTS.md). Mã giao dịch hoàn trùng ⇒ 409, refund giữ `Processing`.

### 5.4 Khoá & đồng thời
Toàn bộ approve/apply trong `ICommercialTransactionRunner` (applock thương mại) + `GetRefundForUpdateAsync` (UPDLOCK). Điều kiện kiểm tra lại **trong khoá**: refund vẫn `Processing` và `GatewayReference` null, hồ sơ vẫn `PendingApproval`, tổng đã cam kết ≤ đã thu. `RowVersion` trên hồ sơ và refund. Hai Admin duyệt đồng thời ⇒ đúng một `Applied`, người kia nhận kết quả cũ/409. Payout thủ công cho B chính là refund thay thế `Requested` tạo nguyên tử ở 3.4 (unique `ReplacesRefundId`), không có bản ghi payout riêng thứ hai.
Email hoàn tiền qua outbox đúng một lần cho A (đường hiện có).

## 6. Test hồi quy xác định (SQL Server thật; HTTP + repository)
Không dùng `Task.Delay`/thời gian thực; thời gian qua `TimeProvider`/tham số; gateway giả đếm lời gọi.
1. **Timeout → Processing:** gateway giả timeout; `GatewayReference` null; `Calls==1`.
2. **A thành công:** hồ sơ khớp đủ 3.1 + hai người ⇒ `Succeeded`, `GatewayReference` set, outbox email đúng 1, gateway `RefundAsync` **không** gọi lần nữa, audit có Opened/Approved/Applied.
3. **Từ chối/mismatch (bảng test):** sai `RequestId`; sai `TxnRef`; sai số tiền; sai tiền tệ; ngày ngoài cửa sổ; thiếu trường; hash tài liệu thiếu; mã giao dịch hoàn trùng refund khác ⇒ trạng thái giữ `Processing`, hồ sơ không `Applied`, audit ghi lý do.
4. **Hai người:** mở và duyệt cùng một user ⇒ bị chặn; Admin thứ hai ⇒ thành công.
5. **Đồng thời:** hai Admin duyệt cùng hồ sơ song song (≥ 8 vòng lặp) ⇒ đúng 1 `Applied`; hai hồ sơ cùng refund ⇒ chỉ 1 `PendingApproval`; không refund nào vượt tổng đã thu.
6. **Idempotency:** cùng key hai lần ⇒ cùng kết quả, không tác dụng phụ; cùng key khác nội dung ⇒ 409.
7. **Không trả tiền hai lần:** sau A, mọi request refund/manual cho cùng booking ⇒ từ chối; sau B + payout được ủy quyền, ủy quyền thứ hai/payout thứ hai ⇒ từ chối; tổng ≤ đã thu.
7a. **Characterization (viết trước, chạy trên code hiện tại):** chỉ đổi refund Processing → `Failed` bằng tay rồi gọi `RequestRefund` ⇒ chứng minh lỗ hổng (tạo refund mới + gateway giả `Calls` tăng). Test này xác nhận *vì sao* B phải nguyên tử; sau triển khai nó trở thành test kỳ vọng ngược lại qua đường B.
7b. **B nguyên tử:** chèn lỗi giả (exception sau bước tạo refund thay thế, trước bước đổi refund cũ; và lần khác sau bước đổi) ⇒ rollback toàn bộ: refund cũ vẫn `Processing`, không có refund thay thế, không đổi allocation, hồ sơ vẫn `PendingApproval`. Kiểm tra bằng kết nối thứ hai (READ COMMITTED) rằng ở **mọi** thời điểm quan sát được, `GetActiveRefundForBooking` ≠ null và `GetCommittedRefundTotal` ≥ số tiền, `GetAllocatedRefundTicketIds` chứa đủ vé (không có khoảng trống).
7c. **Sau B, đường thường bị chặn:** gọi `RequestRefund` (cùng booking) ⇒ trả refund thay thế `Requested`, gateway giả `Calls` không tăng; thử refund thêm vé khác vượt `payment.Amount − committed` ⇒ từ chối (tổng bao gồm refund thay thế); vé đã phân bổ không hoàn lại được.
7d. **Đồng thời B × RequestRefund:** `Task.WhenAll` (≥ 8 vòng; đồng bộ bằng barrier, không `Delay`) giữa approve B và N request refund thường ⇒ đúng một refund hoạt động trên booking; gateway giả không gọi thêm; không có refund thứ hai nào ở `Requested/Processing/Succeeded`; không vi phạm unique index.
7e. **Đồng thời B × B và duyệt lặp:** hai Admin duyệt cùng hồ sơ ⇒ đúng một refund thay thế (unique `ReplacesRefundId/SuccessorRefundId`); hai hồ sơ cho cùng refund ⇒ một `PendingApproval`.
7f. **Idempotency khoá refund:** refund thay thế dùng `IdempotencyKey` khác `REFUND_{booking:N}`; tạo lại ⇒ không va unique; apply lặp ⇒ kết quả cũ.
7g. **Chỉ một payout:** `ManualRefundService.ConfirmAsync` hai lần/đồng thời trên refund thay thế ⇒ một `Succeeded`, một email outbox; không tạo payout thứ hai; refund cũ `Failed` không thể xác nhận thủ công.
7h. **Thực thi muộn sau B:** mô phỏng tín hiệu "lệnh gốc thành công" sau `Applied` ⇒ hồ sơ `Disputed`, refund thay thế bị khoá (nếu chưa `Succeeded`) hoặc sự cố ghi nhận (nếu đã); không tự đảo tiền, không gọi gateway.
7i. **09 không đủ:** `querydr` giả trả 09 + mọi trường khớp nhưng thiếu bằng chứng VNPay gắn đúng `RequestId` ⇒ B bị từ chối, vẫn `Processing`. Bằng chứng có `TxnRef`+số tiền nhưng không có `RequestId` ⇒ từ chối.
8. **Ambiguous giữ Processing:** `querydr` giả trả 05/06, hoặc trả 02/03 thành công trái với kết cục B, hoặc chữ ký sai, hoặc nhiều refund một phần cùng TxnRef cùng số tiền ⇒ `Processing`, không retry, không refund mới.
9. **`T` không đủ:** qua 24h mà không có bằng chứng ⇒ vẫn `Processing`; trước `T` kết cục B bị từ chối.
10. **Audit bất biến:** UPDATE/DELETE trên `RefundReconciliationEvents` thất bại ở mức DB.
11. **Phân quyền:** Passenger/Guest/Staff/Captain ⇒ 403; không token ⇒ 401; Admin không có `FinanceApprover` (nếu chọn tách) ⇒ 403 ở approve.
12. **Rollback:** lỗi giữa chừng trong khoá ⇒ không có trạng thái nửa vời (refund, hồ sơ, audit nhất quán).
13. **Migration:** chỉ mục unique tạo thành công trên dữ liệu sạch; bị chặn có thông báo khi có trùng.

## 7. Giả định CHƯA xác minh (liệt kê chính xác)
### 7.A Phụ thuộc nhà cung cấp (VNPay) — báo cáo riêng
1. U1: `querydr` tra được bản ghi hoàn theo `vnp_TransactionNo` của refund — chưa xác minh, và đặc tả gợi ý ngược lại (V2, V3).
2. U2: hành vi `querydr` khi nhiều refund một phần cùng `TxnRef`.
3. U3: hành vi gửi lại `refund` cùng `vnp_RequestId` (kết quả cũ, 94, hay tạo mới).
4. U4: cổng quản trị/file đối soát VNPay tra được theo `RequestId` hoặc theo (TxnRef, số tiền, thời điểm), và xuất ra đủ trường 3.1; thời gian VNPay lưu dữ liệu.
5. VNPay có xác nhận bằng văn bản chính thức cho từng trường hợp timeout (quy trình hỗ trợ, SLA) — chưa có.
6. Sandbox hành xử giống production — không bảo đảm.
7. `vnp_TransactionStatus=09` **không** được coi là bằng chứng "không thực hiện" (xem 3.3); ý nghĩa chính xác của 09 và việc nó có gắn với một `RequestId` hay không vẫn chưa xác minh.
8. VNPay có thể phát hành bằng chứng nêu đúng `RequestId` và khẳng định lệnh "không còn có thể thực thi muộn" (3.3a) — chưa biết có tồn tại, định dạng, thời hạn phát hành.
9. Một lệnh refund bị VNPay bỏ/từ chối có thể được thực thi muộn hay không (cơ sở của rủi ro 3.4 "thực thi muộn").
10. Thời gian VNPay lưu dữ liệu giao dịch để đối chiếu; cách VNPay xử lý khi `RequestId` bị gửi lại sau nhiều ngày.

### 7.B Giả định nội bộ (không phụ thuộc VNPay)
1. Cửa sổ thời gian ngày giao dịch hoàn 3.1 (`[−5 phút, +T_max]`) là tham số đề xuất, chưa có số liệu.
2. Cơ chế bất biến DB (trigger `INSTEAD OF` hay quyền INSERT-only) khả thi trong môi trường triển khai.
3. Nhóm có ≥ 2 Admin tách nhiệm và người có `FinanceApprover`.
4. BR v2.0 có cho phép nhiều refund hoạt động trên cùng booking hay không (quyết định chỉ mục unique 3.4); dữ liệu hiện có không vi phạm chỉ mục đề xuất.
5. `RefundTicketAllocation` có thể chuyển/sao chép sang refund thay thế mà không vi phạm ràng buộc hiện có (cần kiểm tra khi triển khai; đây là thay đổi kèm migration/ràng buộc nếu có unique theo vé).
6. Không nơi nào khác (báo cáo, đối soát, email outbox) giả định "mỗi booking chỉ có một refund theo thời gian" khi refund cũ `Failed` có refund thay thế.

## 8. Phạm vi KHÔNG làm ở C3
Không thay `GatewayReference` tự động; không gửi lại refund qua gateway; không đổi trạng thái theo thời gian; không dùng số tiền làm bằng chứng; không dựa vào `querydr` theo mã hoàn khi chưa xác minh U1; không suy đoán hành vi VNPay.

## 9. Quyết định nghiệp vụ — mặc định thiết kế đề xuất (KHÔNG phải ủy quyền chi tiền)
| # | Quyết định | Mặc định đề xuất cho bản thiết kế |
|---|---|---|
| 1 | Người thao tác | Hai Admin **khác nhau**; người duyệt có quyền `FinanceApprover` |
| 2 | `T` | 24 giờ — **chỉ là ngưỡng rà soát**, không là bằng chứng |
| 3 | Sau B | **Chỉ payout thủ công**; không bao giờ gửi lại gateway mù quáng |
| 4 | Bằng chứng | Bằng chứng có thẩm quyền của VNPay gắn đúng refund request (`RequestId`) |
| 5 | Audit | Append-only **được DB cưỡng chế** |
| 6 | Kích hoạt tiền thật | Chỉ sau thí nghiệm sandbox **và** xác nhận chính thức của VNPay |

Còn cần chốt: (a) BR có cho nhiều refund hoạt động trên một booking không (ảnh hưởng chỉ mục 3.4); (b) loại tài liệu VNPay cụ thể được chấp nhận và thời hạn lưu; (c) cơ chế bất biến (trigger hay INSERT-only); (d) đầu mối làm việc với VNPay.

## 10. Danh sách nghiệm thu sẵn sàng triển khai
Chỉ bắt đầu khi mục 9 được duyệt bằng văn bản. Mọi mục dưới đây phải đạt trước khi đánh dấu xong. **Tính năng phải ở trạng thái tắt (feature flag mặc định tắt) trên môi trường có tiền thật cho tới khi mục 10.4 đạt.**

### 10.1 Bổ sung theo review v2 (bắt buộc)
- [ ] `querydr`/`TransactionStatus=09` không bao giờ là điều kiện đủ cho B; B yêu cầu bằng chứng VNPay gắn đúng `RequestId` (test 7i).
- [ ] B là **một** giao dịch nguyên tử: refund thay thế `Requested` + chuyển allocation + refund cũ `Failed` + hồ sơ `Applied` + audit; không có khoảng trống quan sát được ở `GetActiveRefundForBooking`, `GetCommittedRefundTotal`, `GetAllocatedRefundTicketIds` (test 7b).
- [ ] Đường `RequestRefund` thường không thể gọi `IRefundGateway.RefundAsync` sau B; hồ sơ `Applied` được kiểm tra rõ ràng, không chỉ dựa vào trạng thái refund (test 7c, 7d).
- [ ] Refund thay thế dùng `IdempotencyKey` riêng; unique `ReplacesRefundId`/`SuccessorRefundId`; chỉ mục unique refund hoạt động theo booking (hoặc phương án thay thế theo quyết định 9a) kèm kiểm tra dữ liệu hiện có (test 7e, 7f).
- [ ] Một refund thay thế = một payout (test 7g); tín hiệu thực thi muộn ⇒ `Disputed`, không tự đảo tiền (test 7h).
- [ ] Test characterization 7a được viết và ghi lại kết quả trước khi triển khai.
- [ ] **Các chuyển trạng thái tài chính hiện có không đổi** (`RefundTimeoutSafetyTests`, `ApiReliabilitySqlHttpTests`, `PaymentRetrySemanticsTests` và toàn bộ test refund xanh); chỉ thêm đường mới.

### 10.4 Điều kiện kích hoạt tiền thật
- [ ] Thí nghiệm sandbox mục 1.1 đã chạy và ghi lại; U1–U4 đã xác minh hoặc có giới hạn bằng văn bản.
- [ ] VNPay xác nhận chính thức: định dạng bằng chứng có `RequestId`, "không còn thực thi muộn", thời hạn lưu.
- [ ] Hai Admin khác nhau + `FinanceApprover` đã cấp quyền và diễn tập trên staging; runbook được duyệt.

### 10.2 Danh sách chung
- [ ] Mục 9.1–9.6 đã có quyết định ghi lại; U1–U4 đã xác minh hoặc đã ghi rõ giới hạn.
- [ ] Kết quả thí nghiệm sandbox (nếu chạy) hoặc xác nhận VNPay được lưu trong `docs/`, không chứa secret.
- [ ] Migration: bảng `RefundReconciliationEvents` (INSERT-only, bất biến ở DB), hồ sơ đối soát có `RowVersion`, unique filtered index hồ sơ `PendingApproval` / `(RefundId, IdempotencyKey)`, unique filtered index `GatewayReference` của Refund; có bước kiểm tra trùng trước khi tạo; `has-pending-model-changes` sạch; không sửa migration đã merge.
- [ ] Service theo pattern `Controller → IService → Service → IRepository`; không MediatR; lỗi qua exception chuẩn (ValidationException 400, Forbidden 403, NotFound 404, Conflict 409); `[Authorize(Roles=...)]`, `ValidateRequestFilter`.
- [ ] Apply chạy trong `ICommercialTransactionRunner` + `GetRefundForUpdateAsync`; điều kiện kiểm tra lại trong khoá; không gọi gateway `RefundAsync` ở bất kỳ nhánh nào.
- [ ] Hai người: `OpenedBy ≠ ApprovedBy` được ép ở service **và** kiểm tra lại trong khoá.
- [ ] Tương quan 3.1 đầy đủ, từng trường có test lệch; thiếu bằng chứng ⇒ C.
- [ ] 13 nhóm test mục 6 xanh trên SQL Server thật; test đồng thời lặp ≥ 8 vòng; không flaky; được thêm vào danh sách no-skip của `.github/workflows/ci.yml`.
- [ ] Test hiện có (`RefundTimeoutSafetyTests`, `ApiReliabilitySqlHttpTests`, `PaymentRetrySemanticsTests`) vẫn xanh; không đổi chuyển trạng thái ngoài phạm vi.
- [ ] Danh sách `reconciliation-required` có `ageHours`/`overdue`/trạng thái hồ sơ; Swagger và tài liệu cập nhật.
- [ ] Audit: mọi hành động ghi sự kiện; không log email/SĐT/token/payload đầy đủ; bằng chứng lưu hash.
- [ ] Review độc lập (Codex) tập trung vào đồng thời + tiền; mọi phát hiện Critical/High đã xử lý.
- [ ] Build + test BE đầy đủ, FE lint/test/build (nếu chạm FE), Docker build; PR vẫn Draft, mô tả ghi giới hạn U1–U4 còn lại.
- [ ] Runbook vận hành: ai làm gì, bằng chứng gì, xử lý khi VNPay không phản hồi (ở lại C).
