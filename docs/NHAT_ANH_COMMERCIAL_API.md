# Nhật Anh — checkout, payment, refund và QR

Phạm vi backend TV2: `PurchaseOrder → Booking → SeatReservation → Ticket`, Redis CheckoutSession/RedLock, VNPay HMAC-SHA512, refund allocation và QR RSA tĩnh theo Booking. Chuẩn nghiệp vụ là BR v2.0 ngày 2026-09-29.

Giữ ASP.NET Core MVC Controller/API + frontend riêng. Booking, Checkout, Guest Access, Payment/VNPay, Refund và QR đã migrate sang `Controller → IService → Service → IRepository → Repository → EF Core`. Controller chỉ binding request/header/context và trả HTTP; Application Services phối hợp Domain Entities, `IUnitOfWork`, `ICommercialTransactionRunner` và các interface tích hợp. Repository và adapter SQL/Redis/VNPay/RSA nằm ở Infrastructure; các service thương mại không dùng EF hoặc MediatR. Validate request qua `ValidateRequestFilter` tại các action đã đăng ký validator; cache/event checkout chỉ chạy sau commit. Transaction runner là dependency bắt buộc; bản giả chỉ nằm trong test assembly. Các CQRS feature ngoài phạm vi thương mại vẫn giữ nguyên. Domain, EF mappings/migrations, IAM, catalog, frontend/mobile, SignalR hubs và tracking/weather không bị refactor trong lượt này. Xem [báo cáo refactoring](../waterbus-be/docs/COMMERCIAL_REFACTORING_REPORT.md) để biết phạm vi file dùng chung và kết quả kiểm chứng.

## Luồng checkout

1. Tạo hold Draft trước khi nhập người mua, hoặc tạo checkout đầy đủ ở PendingPayment.
2. Giữ token trả về để đọc/sửa chính Order đó. Gửi `X-Guest-Token: <accessToken>` cho các endpoint sở hữu; JWT riêng không thay thế token này. DB chỉ lưu SHA-256 token trong AccessGrant. Không đưa token vào log hoặc QR.
3. Có thể thay tập ghế của một Booking trước khi tạo payment attempt; hạn hold của cả Order giữ nguyên. Muốn bỏ toàn bộ hold thì hủy checkout.
4. Submit Draft với người mua và tên hành khách. Không phát Ticket ở bước này.
5. Tạo URL VNPay: ghi payment attempt trước khi trả URL; lần đầu mở cửa sổ PaymentPending, retry trả cùng merchant reference và deadline.
6. IPN/Querydr đã kiểm chứng xác nhận tiền, phân bổ cho từng Booking/reservation và phát Ticket nguyên tử. Browser return chỉ hiển thị kết quả chữ ký, không xác nhận Order.
7. Mỗi Booking đã phát vé có một QR tĩnh. Round-trip có hai Booking và hai QR, chung một Order/payment.

## API

Các đường dẫn dưới đây có prefix `/api/v1`.

| Method | Path | Quyền / hành vi |
| --- | --- | --- |
| POST | `/bookings` | Công khai, rate limit BookingPolicy; tạo một Booking, HTTP 201 Location trỏ tới `/checkouts/{orderId}` |
| POST | `/checkouts/holds` | Công khai, rate limit BookingPolicy; tạo Draft với 1–2 legs |
| POST | `/checkouts` | Công khai, rate limit BookingPolicy; tạo checkout đầy đủ |
| GET | `/checkouts/{orderId}` | Token của Order; trạng thái Booking, Ticket và giao dịch |
| POST | `/checkouts/{orderId}/submit` | Token; chuyển Draft sang PendingPayment |
| PUT | `/checkouts/{orderId}/bookings/{bookingId}/seats` | Token; thay toàn bộ ghế Booking trước payment attempt |
| DELETE | `/checkouts/{orderId}` | Token; hủy hold chưa thanh toán |
| POST | `/payments/create-vnpay-url` | Token; body `{ "orderId": "GUID" }` |
| GET/POST | `/payments/vnpay-ipn` | Gateway query parameters; xác minh HMAC + merchant + amount |
| GET | `/payments/vnpay-return` | Gateway query parameters; chỉ đọc, không đổi DB |
| POST | `/payments/{orderId}/attempts/{paymentId}/reconcile` | Token; Querydr đúng payment attempt |
| GET | `/checkouts/{orderId}/bookings/{bookingId}/qr` | Token; chỉ Booking Confirmed/Completed đã có Ticket |
| GET | `/checkouts/qr-public-key` | Công khai; public PEM, key id, thuật toán |
| POST | `/refunds/request` | AdminOnly; body `{ "bookingId": "GUID", "reason": "..." }` |
| POST | `/refunds/{refundId}/reconcile` | AdminOnly; đối soát khoản hoàn đang Processing |
| POST | `/refunds/confirm-manual` | AdminOnly; bằng chứng, reference và confirmedAmount chính xác |

API thông thường camelCase. IPN trả **đúng** `{ "RspCode": "00", "Message": "Confirm Success" }` theo giao thức VNPay; replay đã xác nhận trả `02`, chữ ký sai `97`, amount sai `04`. HTTP POST IPN vẫn nhận tham số ở query string, như GET.

Response JSON của `POST /bookings` giữ nguyên. Để đọc tài nguyên tại HTTP 201 Location, gửi `X-Guest-Token` chứa `accessToken` được trả về; token không nằm trong URL.

Ví dụ tạo Draft:

```json
{
  "legs": [
    {
      "tripId": "GUID",
      "seatIds": ["GUID"],
      "boardingStationId": "GUID",
      "disembarkingStationId": "GUID"
    }
  ]
}
```

Một leg có thể gửi `boardingCallId`/`disembarkingCallId` để chọn rõ lần ghé bến, và `passengers: [{ "name": "...", "email": null, "phone": null }]` theo thứ tự seatIds. Không dùng Station.OrderIndex để quyết định đoạn. Tạo checkout đầy đủ thêm `customerName`, `customerEmail`, `customerPhone` ở cấp ngoài cùng. Response trả `orderId`, `quotedTotal`, `currency`, `status`, `expiresAt`, `accessToken`, `bookings`; dùng accessToken cấp Order.

Submit Draft:

```json
{
  "customerName": "Nguoi mua",
  "customerEmail": "buyer@example.com",
  "customerPhone": null,
  "passengers": [
    { "bookingId": "GUID", "seatId": "GUID", "name": "Hanh khach", "email": null, "phone": null }
  ]
}
```

Nếu gửi passengers ở submit thì phải khớp toàn bộ ghế đang giữ theo BookingId+SeatId, không lặp. Bỏ passengers thì dùng tên người mua. Update seats gửi `seatIds` và passengers theo cùng thứ tự. ID từ route và token từ header được truyền riêng cho service; body không thay thế phạm vi Order/Booking hoặc token sở hữu.

## Giữ ghế và concurrency

Hạn DB là nguồn sự thật. CheckoutSession Redis có TTL cùng hạn, không tự gia hạn khi sửa ghế; hết TTL Redis tự mất key, worker checkout riêng dọn reservation quá hạn mỗi 30 giây. Service availability hiện có vẫn coi reservation Pending là chiếm ghế cho đến khi worker đổi trạng thái, nên việc hiện ghế khả dụng có thể chậm tối đa khoảng một chu kỳ dọn. Không sửa thuật toán của TV1 trong lượt này. Chỉ giữ/bán khi các đoạn VisitOrder chồng lấn; đoạn nối tiếp vẫn dùng lại ghế được.

RedLock.net bảo vệ critical section ngắn; mutex được giải phóng sau thao tác. Hold 10 phút mặc định nằm ở session + reservation/deadline, không giữ mutex toàn ghế 10 phút vì sẽ chặn cả các đoạn không chồng lấn. Đây là cách áp dụng BR-HOLD-01/04 thay cho mô tả Redis lock toàn ghế trong hướng dẫn cũ.

Mọi writer TV2 chạy transaction SQL và `sp_getapplock` transaction-owned chung `waterbus:commercial`, cộng kiểm tra overlap và RowVersion. SQL bảo vệ khi Redis mất kết nối hoặc lease Redis hết hạn. Khóa DB hiện là khóa toàn module, phù hợp phạm vi môn học; throughput bị giới hạn nếu nhiều checkout đồng thời. Không giữ khóa DB trong lúc gửi refund/querydr qua mạng. Writer bên ngoài TV2 nếu sửa inventory thương mại phải phối hợp cùng cơ chế, không tự ghi SeatReservation trực tiếp.

Redis một node mặc định phù hợp local. Muốn quorum RedLock cần cấu hình các Redis **độc lập**, ví dụ `RedisLock__Connections__0`, `__1`, `__2`; không coi replica của cùng một master là ba node độc lập. Session cache vẫn dùng `ConnectionStrings__RedisConnection`.

## Tiền, refund và reconciliation

Payment attempt ghi FinancialTransaction(Requested), MerchantReference GUID N, IdempotencyKey và GatewayRequestCreatedAt. VNPay create/expire/transaction dates dùng GMT+7; DB lưu UTC. Chỉ trạng thái hoàn tất có xác nhận, đúng amount và đúng merchant reference mới phát vé. Pending/unknown không đổi thành thành công. Callback đến sau expiry, sau hủy hold hoặc khi Trip không còn sẵn sàng ghi receipt thành công với `ReconciliationRequired`, không hồi sinh ghế/phát vé.

Refund tính tổng **PaidFareSnapshot** của từng Ticket đủ điều kiện. Trip Cancelled/Terminated nhưng khách đã được phục vụ đến bến xuống hoặc Ticket Completed/NoShow thì không hoàn. Phần chưa phục vụ hoặc bị cắt giữa chặng được hoàn 100% allocation đủ điều kiện, không prorate theo khoảng cách; round-trip chỉ xử lý Booking bị ảnh hưởng.

Refund được ghi Processing cùng allocations trước khi gọi gateway, giữ budget dưới transaction. Retry/concurrent request trả cùng refund, không gửi lại mạng. GatewayRequestId là GUID N của refund. Timeout, JSON/chữ ký không xác minh được hoặc gateway đang xử lý giữ Processing để đối soát, tránh chuyển khoản lần hai. Querydr chỉ chốt khi khớp reference, refund transaction number, amount và loại 02/03; query kết quả payment 01 không đủ để xác nhận refund. Nếu không đủ dữ liệu đối soát thì giữ Processing; cần xác minh với merchant/bank, không coi việc chờ lâu là thất bại.

Gateway từ chối chắc chắn chuyển Requested để Admin hoàn thủ công; confirm-manual yêu cầu số tiền chính xác, Admin tồn tại và bằng chứng/reference. Không cho manual race với khoản tự động còn Processing. Receipt muộn không có Ticket có nhánh hoàn toàn bộ receipt, dùng cùng cơ chế idempotency/budget. Receipt dư khi Order đã có Ticket cần đối soát riêng; không dùng nó làm payment gốc của Ticket refund.

## QR và cấu hình

Response QR gồm `payload`, `algorithm: "RS256"`, `keyId`, `credentialVersion`. Payload là compact JWS ba phần base64url, ký RSA SHA-256 PKCS#1 v1.5:

```json
{"alg":"RS256","typ":"JWT","kid":"waterbus-booking-v1"}
{"pbid":"public-booking-id","ver":1,"tid":"trip-guid","iat":1791774000}
```

Payload không chứa PII, tài chính hay access token. iat lấy timestamp đã phát hành, nên fetch lại với cùng key/version cho cùng QR. Scanner verify chữ ký bằng PublicKeyPem SPKI, kiểm tra kid/alg/ver và đối chiếu Booking/Ticket cache hợp lệ. Chữ ký không thay thế trạng thái vé hoặc boarding window. Endpoint preload/cache hiện có của Tuấn cần tích hợp public key/contract RS256 này ở phần mobile riêng; TV2 không sửa scanner hoặc IAM. Khi rotate key, cần giữ key/version cũ theo chiến lược preload của scanner; bản này cung cấp key hiện tại.

Inject trực tiếp các key .NET qua User Secrets hoặc môi trường API:

| Key | Mặc định / yêu cầu |
| --- | --- |
| `Checkout__SessionTtlMinutes` | 10, clamp 1–120 |
| `Checkout__PaymentPendingTtlMinutes` | 15, clamp 1–120, mở một lần |
| `Checkout__AccessTokenTtlDays` | 30, clamp 1–365 |
| `Qr__KeyId` | waterbus-booking-v1 |
| `Qr__PrivateKeyPem` | RSA private PEM >=2048 bit, lưu ở secret store |
| `VnPay__TmnCode`, `VnPay__HashSecret` | Merchant được cấp, không commit |
| `VnPay__BaseUrl`, `VnPay__ReturnUrl`, `VnPay__WebApiUrl` | Pay, browser callback, merchant query/refund endpoint |
| `RedisLock__Connections__N` | Tùy chọn quorum Redis độc lập |

`.env.example` là mẫu cấu hình; Docker Compose hiện có không tự inject các key TV2 mới chỉ vì xuất hiện trong file mẫu. Người phụ trách DevOps bổ sung mapping khi triển khai. Không sinh private key ngẫu nhiên mỗi startup production: QR đã cấp cần key ổn định để verify offline. Không có fallback secret hoặc fake RSA signer.

## Kiểm chứng

Chạy `dotnet build waterbus-be/WaterbusSystem.sln` và `dotnet test waterbus-be/WaterbusSystem.sln`.

Các integration tests dùng SQL Server thật khi có `WATERBUS_TEST_SQLSERVER`; tự tạo/xóa DB `WaterbusDomainTests_*` riêng. Đặt `WATERBUS_TEST_REDIS` thành danh sách Redis độc lập cách nhau bằng `;` để chạy kiểm chứng quorum lock và TTL thật. Không đặt các biến này thì các test tương ứng skip; không được coi skip là đã kiểm chứng integration.

Baseline lịch sử 2026-10-13 (không phải kết quả hiện tại): build 0 warnings/errors; **215 tests pass, 0 fail, 0 skip**, có SQL Server 2022 và ba Redis local riêng. Kiểm chứng concurrent checkout chỉ một winner, rollback round-trip không ghi cache, cache ghi sau khi cả hai Booking commit, concurrent refund chỉ một gateway call, exclusivity/release của RedLock và expiry cache. Smoke HTTP thật kiểm tra Draft → sửa ghế → submit → payment retry → IPN/replay → Ticket → QR verify RSA → Admin refund/retry, có cả nhánh 403/409 và Swagger owner header. Bộ request Postman đã kiểm tra qua HTTP runner, có script giả IPN local. Xem `NHAT_ANH_TEST_GUIDE.md` để test và biết phụ thuộc tích hợp. Merchant refund HTTP dùng gateway giả cục bộ có kiểm chứng HMAC request và ký response; chưa gọi VNPay sandbox/production bằng tài khoản merchant thật. Cần merchant credentials/quyền refund để nghiệm thu với ngân hàng.


Kết quả được báo cáo trước đồng bộ main, sau commit `3b6701b` (2026-10-28): build **0 warnings, 0 errors**; **785 passed, 0 failed, 0 skipped**, gồm **18 SQL/Redis integration tests**. Commit này sửa HTTP 201 Location của POST Booking tới Checkout GET hiện có và bổ sung 5 HTTP tests dùng chữ ký VNPay thật cho POST query-string IPN (hợp lệ, sai chữ ký, replay, form-only và form override). Đây là kết quả lịch sử trước merge; kết quả hậu đồng bộ được ghi riêng trong [báo cáo refactoring](../waterbus-be/docs/COMMERCIAL_REFACTORING_REPORT.md).
