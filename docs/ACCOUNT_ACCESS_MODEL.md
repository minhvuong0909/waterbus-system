# Mô hình truy cập Guest / Passenger / Ticket (BR-ID-01..07, BR-QR, BR-NOT-02)

Nguồn: BR v2.0 mục 7. Tài liệu này mô tả hợp đồng API và bất biến bảo mật của Task 4.

## 1. Ba cách chứng minh quyền

| Chủ thể | Bằng chứng | Phạm vi |
|---|---|---|
| Guest (Purchaser) | `X-Guest-Token` = **Manage Order token** (256-bit ngẫu nhiên, DB chỉ lưu SHA-256) | Đúng một Order: xem đơn, QR, vé, huỷ/đổi ghế khi còn Draft, tạo TripAccess |
| Passenger đã liên kết | JWT (role `Passenger`) **và** `PurchaseOrder.PassengerAccountId == sub` | Mọi route `/checkouts/{orderId}/...` của Order mình sở hữu, không cần token; `/me/*` |
| Người xem chuyến (hành khách khác) | `X-Trip-Access-Token` = **TripAccess token** (`AccessType = PassengerTrip`, gắn một `BookingId`) | Chỉ `GET /api/v1/tripaccess` — thông tin hành trình |

BR-ID-04 trong BR v2.0 yêu cầu dùng token để claim từng đơn. Mục 5a ghi đề xuất bổ sung việc gắn tự động theo email đã xác minh và điều kiện để áp dụng.

## 2. Endpoint

| Method & route | Auth | Ghi chú |
|---|---|---|
| `POST /checkouts/{orderId}/claim` | JWT `Passenger` **+** `X-Guest-Token` hợp lệ của chính Order | Gọi lặp cùng tài khoản → 200 `alreadyLinked=true`; tài khoản khác → 409; thiếu/sai token → 403 |
| `GET /me/orders?pageNumber&pageSize` | JWT `Passenger` | Chỉ Order của tài khoản, không có Draft; `pageSize` 1–100 |
| `GET /me/orders/linkable` | JWT `Passenger`, tài khoản đang hoạt động có email đã xác minh | Đếm đơn Guest đủ điều kiện (đề xuất tại mục 5a) |
| `POST /me/orders/link-by-email` | JWT `Passenger`, tài khoản đang hoạt động có email đã xác minh | Gắn tối đa 200 đơn mỗi lần theo email của tài khoản lưu trên server, trả `linked` và `remaining` (đề xuất tại mục 5a) |
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

## 5a. Đăng ký khách hàng và gắn đơn Guest theo email đã xác minh
**Đề xuất sửa BR-ID-04, cần nhóm phê duyệt trước khi merge vào main.** Sau khi Passenger xác minh an toàn quyền kiểm soát email, hệ thống có thể tự gắn các Order Guest đủ điều kiện đã mua bằng cùng email khi Passenger mở lịch sử đơn. Phải bảo vệ quyền sở hữu hiện có, trạng thái thanh toán, quyền truy cập bằng token Guest và tính toàn vẹn giao dịch.

Đề xuất chấp nhận quyền kiểm soát hộp thư nhận thông tin mua vé làm bằng chứng để gắn các đơn mua bằng hộp thư đó. Với hộp thư dùng chung hoặc được cấp lại, người đang kiểm soát hộp thư có thể nhận lịch sử đủ điều kiện của người mua trước. Claim từng đơn qua `POST /checkouts/{orderId}/claim` vẫn yêu cầu Manage Order token hợp lệ của đơn đó.

Luồng: Guest mua vé → đăng ký bằng email người mua → mở liên kết xác minh trong hộp thư và đặt mật khẩu → đăng nhập → mở `/account/orders` để tự động gắn đơn và cập nhật lịch sử.
- `POST /auth/register` tạo Passenger chờ kích hoạt với `email`, `fullName` và `phoneNumber` (tùy chọn). Yêu cầu đăng ký không đặt mật khẩu. Phản hồi đăng ký và gửi lại giống nhau giữa email mới và đã tồn tại, với ngưỡng thời gian phản hồi tối thiểu 400 ms.
- `POST /auth/confirm-email` nhận `userId`, `token` và `password`. Token xác minh của ASP.NET Identity có hiệu lực 24 giờ. Identity kiểm tra token và mật khẩu rồi lưu xác minh và hash mới trong một lần cập nhật, thay cả hash của tài khoản cũ chưa xác minh. Security stamp mới vô hiệu hóa các liên kết đã gửi. Hai yêu cầu kích hoạt đồng thời chỉ một yêu cầu có thể lưu thành công.
- Khách hàng (Passenger) chưa xác minh email thì không đăng nhập được; tài khoản nhân sự (Admin, Captain, Staff) không bị áp quy tắc này.
- Email xác minh gửi qua `IEmailTransport` bằng hàng đợi trong bộ nhớ tối đa 200 thư. Cần `Email:Enabled=true`, SMTP hợp lệ và `Email:PublicBaseUrl` (biến `PUBLIC_URL`). URL công khai phải dùng HTTPS, HTTP chỉ dùng với địa chỉ loopback. Liên kết mới dùng fragment `/verify-email#uid=…&token=…`. Web thay URL hiện tại ngay khi đọc và chỉ giữ token trong bộ nhớ. Liên kết query cũ vẫn mở được trang đặt mật khẩu. Nginx bỏ access log và gửi `Referrer-Policy: no-referrer` cho trang này.
- Hàng đợi từ chối thư mới khi đầy và ghi cảnh báo. Lỗi SMTP được ghi theo loại lỗi, không ghi nội dung thư hay liên kết. Thư có thể mất khi khởi động lại tiến trình. Người dùng có thể gọi `POST /auth/resend-verification` để lấy liên kết mới. Thời gian chờ gửi lại là 60 giây theo tài khoản (Redis, mất Redis thì vẫn gửi và dựa vào rate limit theo IP).
- Gắn đơn chỉ với đơn Guest ở trạng thái `Confirmed`, `Cancelled`, `Expired`. Đơn hủy hoặc hết hạn là lịch sử đơn hàng, trạng thái vé được hiển thị riêng. `ICommercialTransactionRunner` và RowVersion bảo vệ quyền sở hữu khi gắn đồng thời hoặc claim bằng token. Thao tác gắn chỉ đổi chủ sở hữu, không phát vé và không thay đổi thanh toán, hoàn tiền hoặc hiệu lực Manage Order token.
- Web kiểm tra số đơn đủ điều kiện, tự gọi POST nếu có, chỉ gọi thêm khi `remaining > 0` và có tiến triển, tối đa 10 lượt mỗi lần chạy. Lịch sử đọc lại sau khi gắn hoặc sau lỗi có thể xảy ra sau khi giao dịch đã commit. Nếu còn đơn sau giới hạn hoặc gặp lỗi, giao diện báo lịch sử có thể thiếu và cho thử lại.
- Hồ sơ: `GET/PUT /me/profile` (họ tên, số điện thoại; email không đổi qua đây).
- Mật khẩu tối thiểu 8 ký tự (chữ hoa, chữ thường, chữ số). Role luôn được tạo khi khởi động (mọi môi trường), không chỉ khi seed ở Development.
- Migration `AddPurchaseOrderLinkingIndexes`: index lọc `PurchaserEmail` (chỉ đơn chưa gắn tài khoản) và `(PassengerAccountId, CreatedAt DESC)`. Bảng có index lọc nên câu lệnh SQL chạy tay phải bật `QUOTED_IDENTIFIER` (`sqlcmd -I`); ứng dụng (SqlClient) mặc định đã bật.
- So khớp email dùng điều kiện có thể tận dụng index trên SQL Server, sau đó so sánh nhị phân trên giá trị đã đổi hoa để loại các địa chỉ khác do collation coi là tương đương. Token xác minh dùng khoá DataProtection. Docker Compose giữ khoá trong volume `dataprotection_keys`; nếu mất khoá thì người dùng cần yêu cầu gửi lại.
- Triển khai web và API cùng phiên bản. Nếu bản cũ của PR #85 từng được triển khai, ngừng phục vụ endpoint xác nhận cũ khi chuyển phiên bản. Rà soát các tài khoản đã kích hoạt qua bản cũ và xử lý lại thông tin đăng nhập trước khi mở gắn đơn tự động cho các tài khoản đó.

## 6. Giới hạn đã biết / quyết định
- **Đăng ký tài khoản Passenger không nằm trong BR v2.0.** Đã thêm endpoint đăng ký và xác minh email, xem mục 5a (đề xuất sửa BR-ID-04, chờ nhóm xác nhận).
- `AccessGrant.LastUsedAt` chưa được ghi (tránh ghi DB ở mỗi lần đọc).
- Legacy `Booking.ManageOrderTokenHash` vẫn được middleware nhận diện như cũ, không dùng cho route mới.
- Rate limit chống dò token dùng chính entropy 256-bit; chưa có policy rate-limit riêng cho các route mới.
