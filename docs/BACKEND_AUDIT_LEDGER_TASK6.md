# Task 6 — Audit ledger (API, reliability & architecture polish)

Phạm vi: rà soát có mục tiêu theo BR v2.0 FINAL, không viết lại codebase. Mỗi dòng ghi **bằng chứng** và **mức xác minh**:
- **Test** = có test tái hiện/kiểm chứng trong PR này (SQL Server thật / HTTP thật khi nói rõ).
- **Đọc code** = chỉ đọc mã (tĩnh), CHƯA có test tái hiện; không đổi logic production dựa trên mức này.

Trạng thái: `Fixed` (lỗi xác nhận, đã sửa) · `Resolved` (đã được xử lý từ trước) · `Backlog` (xác nhận còn, chưa sửa trong PR này) · `Out of scope`.

## A. Backlog đã chấp nhận từ Task 1–3

| # | Mục | Trạng thái | Bằng chứng |
|---|---|---|---|
| A1 | SearchTrips không chặn paging/cửa sổ ngày | **Fixed** | `TripService.SearchAsync`: `pageSize` mặc định = tối đa = 100 (validator 1–100), không có ngày ⇒ cửa sổ 14 ngày kể từ hiện tại, tập ứng viên DB tối đa 1000 (`ORDER BY DepartureTime, Id`) **sau khi** lọc bến lên/xuống trong SQL (chuyến không phục vụ bến được tìm không chiếm giới hạn — test với 1005 chuyến gây nhiễu); thứ tự lên/xuống, tồn kho, giá vẫn xét chính xác ở service. Còn giới hạn: nếu >1000 chuyến CÙNG khớp bến trong một cửa sổ thì chỉ 1000 chuyến sớm nhất được xét. Test (SQL+HTTP): 130 chuyến ⇒ trang 1 = 100, trang 2 = 30, chuyến +30 ngày bị loại khi không có ngày nhưng tìm được khi truyền ngày, `pageSize=101`/`page=0` ⇒ 400. **Thay đổi hợp đồng**: client không gửi `pageSize` trước đây nhận toàn bộ, nay tối đa 100/trang (`page=2`…). Caller đã rà: FE luôn gửi `departureDate` + cặp bến (một ngày, thường <100 chuyến) nên không đổi; `TripSearchTool` (AI) còn lọc giờ cục bộ nên nay gom tối đa 5 trang × 100. |
| A2 | `Boat.TotalSeats` cập nhật đồng thời với kích hoạt/thêm ghế | **Fixed** (mức an toàn) | `BoatService.UpdateAsync` chạy trong `ICommercialTransactionRunner` như `BoatSeatService` (đếm ghế + ghi TotalSeats là một đơn vị, lỗi đua là 409 sạch thay vì exception thô). Test (SQL, 6 vòng đua 2 DbContext): bất biến ghế hoạt động ≤ TotalSeats luôn đúng và bên thua nhận `ConflictException/ConcurrencyException`. Lưu ý trung thực: `RowVersion` của `Boat` đã chặn vi phạm bất biến từ trước; test này khoá thêm kiểu lỗi trả về, không chứng minh đã từng sai dữ liệu. |
| A3 | Đổi tàu khi đã có ghế bán (BR-SCH-07) qua HTTP + SQL | **Resolved + test bổ sung** | Test (SQL+HTTP): tàu thiếu ghế tương thích ⇒ 409 và đặt chỗ/Trip không đổi; tàu tương thích ⇒ 200, `remappedReservations=1`, ghế đổi sang cùng `SeatCode`+`SeatClass` của tàu mới. |
| A4 | Trip ad-hoc (BR-SCH-10) | **Fixed** — BR là `[CHỐT]` và chưa có API | `POST /api/v1/admin/trips` (AdminOnly): Route, loại dịch vụ (khớp Route), các bến thực sự phục vụ (VisitOrder = thứ tự, cho phép lặp bến), giờ khởi hành tương lai, mốc đóng bán/check-in; `ScheduleId = null`, Trip chưa có tàu ⇒ không bán được cho đến khi `PUT /admin/trips/{id}/boat` (BR-SCH-04, kiểm tra trùng lịch tàu BR-SCH-08 ở bước gán). Ràng buộc thêm theo review: bến đầu rời đúng offset 0, bến đầu cho lên/bến cuối cho xuống, phần tử stop null → 400. Chưa kiểm tra chiều đi theo `SequenceNo` (cho phép tuyến vòng) và `departureTime` không kèm offset được bind theo múi giờ máy chủ (**Backlog**, khuyến nghị client luôn gửi offset/`Z`). Test (SQL+HTTP): 401/403/400/404/409, không ghi dở khi lỗi, dữ liệu lưu đúng, không xuất hiện trong Search trước khi gán tàu và xuất hiện sau đó. |
| A5 | FE `getTripDetail` dùng thời lượng cả chuyến cho chặng không có lịch bến | **Out of scope (FE dependency)** | Ghi nhận là việc FE riêng, không gộp vào Backend. |

## B. Hợp đồng API

| # | Mục | Trạng thái | Bằng chứng |
|---|---|---|---|
| B1 | `vnp_IpAddr` dạng IPv6-mapped (`::ffff:a.b.c.d`) | **Fixed** | `BaseApiController.ClientIp()/ToIpv4String` dùng cho Payments/Refunds. Test đơn vị 4 trường hợp. (Giữ nguyên IP cho rate limit.) |
| B2 | ProblemDetails/401/403/404/409 cho endpoint mới | **Test** | Các test HTTP của Task 4/5/6 kiểm tra 400/401/403/404/409 trên route mới. |
| B3 | Quyền sở hữu tài nguyên | **Resolved (Task 4/5)** | Xem `ACCOUNT_ACCESS_MODEL.md`, `SIGHTSEEING_AUDIO_PLAYBACK.md`. |

## C. Thanh toán & hoàn tiền (đối chiếu `docs/KNOWN_ISSUES.md`)

Các dòng dưới đây xác minh bằng **đọc code** (rà soát tĩnh); chưa tái hiện bằng test nên **không đổi logic tiền** trong PR này.

| # | Mục | Trạng thái | Ghi chú |
|---|---|---|---|
| C1 | Callback thành công sau callback thất bại cùng mã bị idempotency bỏ qua | **Resolved** | `PaymentConfirmationService` ghi `ReconciliationRequired`; đã có test `PaymentOutcomeTests` (Failure→Success, duplicate, conflict, late success). |
| C2 | VNPay HashSecret/TmnCode rỗng bị chấp nhận | **Resolved (lazy)** | `RequireSetting` dùng `IsNullOrWhiteSpace` và ném lỗi ở mọi đường dùng ⇒ IPN giả không xác nhận được đơn. PR này thêm **cảnh báo khi khởi động** (`Program.cs`) cho `VnPay:TmnCode`, `VnPay:HashSecret`, `Qr:PrivateKeyPem`. Không fail-fast vì `docker-compose.yml` mặc định các khoá này rỗng và chạy `Production`; fail-fast sẽ làm hỏng `docker compose up` khi chưa có `.env` — **quyết định cần người review**. |
| C3 | Refund timeout chưa có `GatewayReference` kẹt `Processing` | **Confirmed — cần quyết định nghiệp vụ** (an toàn tiền, thiếu lối thoát vận hành) | Tái hiện bằng test (`RefundTimeoutSafetyTests`, đã có + `ReconciliationList_…` trên SQL thật): sau timeout refund ở `Processing`, `GatewayReference` null; `ReconcileAsync` chỉ khớp khi `TransactionNo == GatewayReference` nên không bao giờ hội tụ; `confirm-manual` bị chặn cho Processing của gateway tự động; yêu cầu hoàn lại trả đúng refund cũ (không hoàn trùng, BR-PAY-03 đạt). **Không có cách tương quan an toàn**: querydr VNPay không trả `vnp_RequestId`, nên chấp nhận `TransactionNo` lạ chỉ theo số tiền có thể gán nhầm khi có nhiều refund một phần trên cùng payment (BR-PAY-06) ⇒ KHÔNG tự chuyển Succeeded. Mọi lối thoát (Admin xác nhận sau khi đối chiếu cổng VNPay ngoài hệ thống / đánh dấu "chưa hoàn" để hoàn thủ công) thay đổi quy trình tiền và BR-RF-10 chưa quy định cho trường hợp timeout ⇒ cần quyết định BR. Đã làm: Admin thấy các refund này ở `GET /api/v1/refunds/reconciliation-required` (kind `RefundProcessing`). **Chưa sửa logic**; hướng đã chọn (đối soát thủ công có bằng chứng) và các câu hỏi cần duyệt nằm ở `docs/C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md` (đã đối chiếu đặc tả VNPay: querydr không trả `vnp_RequestId`, một bản ghi mỗi `TxnRef`). Mức: rủi ro vận hành production refund, không phải mất/trùng tiền. |
| C4 | Payment retry / dùng lại `vnp_TxnRef` (BR-PAY-03) | **Disproven là lỗi tái dùng TxnRef; retry cùng Order = quyết định sản phẩm (backlog chấp nhận)** | `PaymentRetrySemanticsTests`: mở lại URL khi attempt còn `Requested` dùng cùng attempt/TxnRef (cố ý, idempotent); sau IPN thất bại Order bị huỷ, ghế nhả, `CreateVnPayUrlAsync` cùng Order ⇒ 409, KHÔNG phát hành lại TxnRef cũ; thử lại = checkout mới (Order/attempt/TxnRef mới). BR-PAY-03 "mỗi lần thử là một transaction record" được thoả. Cho phép nhiều attempt trên cùng Order sau thất bại sẽ đổi vòng đời Order/ghế (BR-PAY-05 không yêu cầu) ⇒ không làm. |
| C5 | Tiền về muộn: thiếu workflow Admin cho `ReconciliationRequired` | **Fixed (phần phát hiện); phần hoàn tiền đã có từ trước** | Đường hoàn tiền: `PaymentOutcomeTests.FailureThenVerifiedSuccess…` (late success → refund `LATE_REFUND_`, retry trả cùng refund, gateway gọi 1 lần). Thiếu: không có cách tìm các khoản này. Thêm `GET /api/v1/refunds/reconciliation-required` (AdminOnly, phân trang, chỉ đọc): `LatePayment` (kèm `bookingId` để gọi `POST /refunds/request`, `refundId/refundStatus` nếu đã có) và `RefundProcessing`. Test SQL thật + HTTP: 401/403/200, nội dung, phân trang. Không tự động hoàn tiền (BR-PAY-05: cần reconciliation trước khi quyết định). |
| C6 | `UseHttpsRedirection` áp dụng cho IPN HTTP; IPN chưa có rate limit / log query đầy đủ | **Backlog** (cấu hình triển khai) | Cần kiểm tra thực tế sau nginx; không sửa tại đây. |
| C7 | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` không có danh sách proxy tin cậy | **Backlog** (cấu hình triển khai) | Cổng API bind 127.0.0.1 trong compose nên rủi ro hạn chế. |
| C8 | VNPay: so sánh chữ ký / số tiền / return URL | **Resolved** | `FixedTimeEquals`, số tiền so với giá trị lưu (mã 04), `ReturnUrl` chỉ từ cấu hình (không open redirect). |

## D. Độ tin cậy & tích hợp

| # | Mục | Trạng thái | Ghi chú |
|---|---|---|---|
| D1 | SQL concurrency của thương mại | **Resolved** | Khoá applock thương mại toàn cục (có chủ ý, thông lượng thấp hơn nhưng đúng); mở rộng sang `BoatService.UpdateAsync` (A2). |
| D2 | Redis down | **Resolved (đọc code)** | Khoá phân tán rơi về lease bảo vệ bởi SQL; hold/release bọc try-catch; còn `catch` nuốt lỗi không log trong `PaymentService`/`PaymentConfirmationService` (**Backlog**, chỉ ảnh hưởng cache TTL). |
| D3 | Background jobs / email outbox | **Resolved (đọc code)** | Claim `UPDLOCK, READPAST` + lease + token; Weather/NoShow có applock + `DisableConcurrentExecution`. |
| D4 | `BoatTrackingHub.JoinBoat` không kiểm tra quyền | **Backlog (chấp nhận)** | FE dùng `JoinBoat` cho theo dõi chuyến của hành khách (`useTripTracking`), nên chặn theo role sẽ làm hỏng tính năng. Dữ liệu chỉ là vị trí tàu. Hướng sửa: cho phép Passenger chỉ khi tàu thuộc chuyến của họ (cần Task 4 + quyết định BR). |
| D5 | Cấu hình/migration/triển khai | **Resolved** | Không có migration mới trong PR này. |
| D6 | Tài liệu `KNOWN_ISSUES.md` lỗi thời | **Fixed** | Cập nhật các dòng Search paging và API tạo chuyến theo bảng trên. |

## Ngữ nghĩa chính xác của giới hạn 1000 ứng viên trong Search

`POST/GET trips/search` KHÔNG đảm bảo phân trang đầy đủ trên mọi chuyến khớp:
1. Cửa sổ ngày: không truyền ngày ⇒ chỉ 14 ngày kể từ hiện tại; chuyến ngoài cửa sổ không xuất hiện.
2. SQL lấy tối đa 1000 chuyến, sắp `DepartureTime, Id`, đã lọc "có phục vụ bến lên VÀ bến xuống" nhưng CHƯA lọc thứ tự bến, ghế còn trống, giá.
3. Service lọc tiếp (chiều đi, tồn kho, giá) rồi mới phân trang trên tập đã lọc ⇒ `totalCount`/số trang chỉ phản ánh tập trong 1000 này.
4. Hệ quả: nếu trong cửa sổ có >1000 chuyến cùng phục vụ cặp bến, các chuyến SAU chuyến thứ 1000 bị bỏ qua, kể cả khi 1000 chuyến đầu đều hết chỗ/sai chiều (kết quả có thể rỗng dù còn chuyến bán được muộn hơn). Cách tránh: truyền `departureDate` hẹp (FE đã làm).
5. Không có tín hiệu "đã cắt" trong response (backlog: thêm cờ `truncated`).
