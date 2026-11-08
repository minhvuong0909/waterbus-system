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
| C3 | Refund timeout chưa có `GatewayReference` kẹt `Processing` | **Backlog** (đọc code: còn) | `RefundReconciliationService` yêu cầu reference; `ManualRefundService` chặn Processing của gateway tự động. Cần quyết định nghiệp vụ (BR-RF-07/10): chấp nhận `TransactionNo` từ querydr khi khớp TxnRef/số tiền, hoặc đường manual sau khi querydr xác nhận chưa có refund. Chưa tái hiện nên chưa sửa. |
| C4 | Payment retry: IPN thất bại huỷ Order, dùng lại `vnp_TxnRef` của attempt Requested (BR-PAY-03) | **Backlog** (đọc code: còn) | Đổi vòng đời Order/attempt ảnh hưởng tiền và ghế; cần quyết định BR + test tái hiện trước. |
| C5 | Tiền về muộn: chưa có danh sách Admin các khoản `ReconciliationRequired` | **Backlog** | Đường refund `LATE_REFUND_` đã có; thiếu endpoint liệt kê. |
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
