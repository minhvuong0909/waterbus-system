# Mô hình truy cập Guest / Passenger / Ticket (BR-ID-01..07, BR-QR, BR-NOT-02)

Nguồn: BR v2.0 mục 7. Tài liệu này mô tả hợp đồng API và bất biến bảo mật của Task 4.

## 1. Ba cách chứng minh quyền

| Chủ thể | Bằng chứng | Phạm vi |
|---|---|---|
| Guest (Purchaser) | `X-Guest-Token` = **Manage Order token** (256-bit ngẫu nhiên, DB chỉ lưu SHA-256) | Đúng một Order: xem đơn, QR, vé, huỷ/đổi ghế khi còn Draft, tạo TripAccess |
| Passenger đã liên kết | JWT (role `Passenger`) **và** `PurchaseOrder.PassengerAccountId == sub` | Mọi route `/checkouts/{orderId}/...` của Order mình sở hữu, không cần token; `/me/*` |
| Người xem chuyến (hành khách khác) | `X-Trip-Access-Token` = **TripAccess token** (`AccessType = PassengerTrip`, gắn một `BookingId`) | Chỉ `GET /api/v1/tripaccess` — thông tin hành trình |

Không có đường nào gán Order cho tài khoản theo **email trùng**. Email chỉ dùng để gửi thông báo.

## 2. Endpoint

| Method & route | Auth | Ghi chú |
|---|---|---|
| `POST /checkouts/{orderId}/claim` | JWT `Passenger` **+** `X-Guest-Token` hợp lệ của chính Order | Gọi lặp cùng tài khoản → 200 `alreadyLinked=true`; tài khoản khác → 409; thiếu/sai token → 403 |
| `GET /me/orders?pageNumber&pageSize` | JWT `Passenger` | Chỉ Order của tài khoản, không có Draft; `pageSize` 1–100 |
| `GET /me/tickets/{ticketId}` | JWT `Passenger` | Vé của người khác → **404** (không lộ sự tồn tại) |
| `GET /checkouts/{orderId}/tickets/{ticketId}` | Token hoặc chủ sở hữu | Vé không thuộc Order → 404 |
| `POST /checkouts/{orderId}/access/rotate` | Token hiện tại hoặc chủ sở hữu | Thu hồi token cũ, cấp token mới (token chỉ trả một lần) |
| `POST /checkouts/{orderId}/access/revoke` | Token hoặc chủ sở hữu | Thu hồi mọi Manage Order + TripAccess link |
| `POST /checkouts/{orderId}/bookings/{bookingId}/trip-access` | Token hoặc chủ sở hữu | Chỉ Booking `Confirmed/Completed`, trả 409 nếu chưa thanh toán |
| `GET /tripaccess` | `X-Trip-Access-Token` | Không có giá, thanh toán, Purchaser, QR, OrderId |
| `GET /checkouts/{orderId}[/bookings/{id}/qr]` (đã có) | Token **hoặc** chủ sở hữu | Hành vi Guest cũ không đổi |

Mã lỗi: 401 chưa đăng nhập (route `Authorize`), 403 sai/thiếu/hết hạn/đã thu hồi token hoặc không sở hữu, 404 tài nguyên không thuộc phạm vi, 409 xung đột sở hữu hoặc trạng thái. Tất cả là ProblemDetails.

Khi Passenger đã đăng nhập tạo checkout (`POST /bookings`, `/checkouts`, `/checkouts/holds`), Order được gán `PassengerAccountId` ngay; Guest để `null`.

## 3. Vòng đời token (BR-ID-02, BR-ID-07)
- Sinh bằng CSPRNG 32 byte; `AccessGrant.TokenHash` UNIQUE; không bao giờ log/lưu token gốc.
- Hạn dùng: `Checkout:AccessTokenTtlDays` (mặc định 30, kẹp 1–365). **Rotation** = `rotate`; **revocation** = `revoke`; chủ sở hữu đã đăng nhập có thể xoay mà không cần token cũ (link bị lộ).
- Mọi thay đổi (claim, rotate, revoke, issue TripAccess) chạy trong `ICommercialTransactionRunner` (SQL applock + transaction). Vì vậy:
  - **Replay**: token cũ bị dùng sau rotate/revoke/hết hạn luôn bị 403; hai rotate đồng thời cùng token chỉ một bên thành công.
  - **Claim đồng thời**: nhiều tài khoản cùng claim → đúng một thắng, còn lại 409; cùng tài khoản gọi nhiều lần → tất cả 200.
  - Lỗi sau khi flush → rollback toàn bộ (test chèn lỗi sau `SaveChanges`).
- Claim **không** thu hồi Manage Order token của Guest (người mua vẫn dùng link cũ); chủ sở hữu có thể `rotate`/`revoke`.

## 4. Tách bạch các trục trạng thái (vé)
`TicketDetailDto` trả riêng: `orderStatus`, `bookingStatus`, `paymentStatus` (của Booking), `ticketStatus`, `boardingStatus`, và `serviceEntitled = ticketStatus == Valid && bookingStatus == Confirmed`. Trạng thái hoàn tiền nằm ở `FinancialTransaction`, không suy ra từ Ticket. Vé chỉ tồn tại sau thanh toán (`Ticket.Issue`), QR chỉ cấp cho Booking đã phát vé (BR-QR-01) — không đổi.

## 5. OneWay / RoundTrip
Claim, lịch sử, rotate, revoke hoạt động ở mức **Order**, nên một thao tác bao phủ cả hai Booking của RoundTrip; mỗi Booking có QR và TripAccess riêng.

## 6. Giới hạn đã biết / quyết định
- **Đăng ký tài khoản Passenger không nằm trong BR v2.0 và chưa có endpoint** (chỉ `POST /auth/login`). Task này không tự đặt quy tắc đăng ký/xác minh email; claim giả định tài khoản Passenger đã tồn tại.
- Không có migration mới: lịch sử lọc theo `PassengerAccountId` (chưa có chỉ mục riêng; thêm khi số Order lớn).
- `AccessGrant.LastUsedAt` chưa được ghi (tránh ghi DB ở mỗi lần đọc).
- Legacy `Booking.ManageOrderTokenHash` vẫn được middleware nhận diện như cũ, không dùng cho route mới.
- Rate limit chống dò token dùng chính entropy 256-bit; chưa có policy rate-limit riêng cho các route mới.
