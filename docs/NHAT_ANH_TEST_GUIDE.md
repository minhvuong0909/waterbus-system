# Nhật Anh — kết quả triển khai và cách test (cập nhật 2026-10-28)

## Kết quả trước đồng bộ main

Sau `3b6701b`, kết quả được báo cáo: **785 passed, 0 failed, 0 skipped**, gồm **18 SQL/Redis integration tests**; build **0 warnings, 0 errors**. Đây là kết quả lịch sử trước merge. Xem [báo cáo refactoring](../waterbus-be/docs/COMMERCIAL_REFACTORING_REPORT.md) cho kết quả hậu đồng bộ.

### Baseline lịch sử 2026-10-13

**Phần backend TV2 đã implement và kiểm thử local.** Build: 0 warnings/errors. Test suite: **215 pass, 0 fail, 0 skip**, có SQL Server 2022 thật và ba Redis độc lập. HTTP smoke pass: Draft, đổi ghế, submit, payment retry, IPN/replay, Ticket, QR RSA, Admin refund và refund retry. Bộ request Postman được kiểm tra bằng HTTP runner: 11 requests + script IPN local đều pass; chưa chạy bằng ứng dụng Postman.

Các test SQL chứng minh hai checkout đồng thời chỉ một winner; round-trip lỗi leg hai rollback toàn bộ và không ghi cache; cache ghi đúng một lần sau khi cả hai Booking đã nhìn thấy từ connection khác; refund đồng thời chỉ gửi một gateway call. Redis tests kiểm tra quorum mutex/release và TTL thật. Test kiến trúc chặn handler phụ thuộc handler khác và chặn transaction runner optional/fake trong assembly sản phẩm.

## Kiến trúc BE

Giữ **ASP.NET Core MVC Controller API + frontend riêng**, với các use case Booking, Checkout, Guest Access, Payment/VNPay, Refund và QR theo:

`Controller → IService → Service → IRepository → Repository → EF Core`

Application Services phối hợp Domain Entities, Unit of Work, SQL commercial transaction và interface tích hợp; không dùng EF/MediatR trong các service thương mại. Infrastructure cung cấp repository, SQL transaction, Redis, VNPay HTTP/HMAC và RSA. Validate request qua `ValidateRequestFilter` tại các action đã đăng ký validator. `CheckoutCommitEffects` cập nhật session/event sau commit; QR/public key qua `IBookingQrService`. Các handler thương mại cũ đã được xóa; CQRS của module ngoài phạm vi vẫn giữ nguyên. Auth đã migrate sang service theo convention của nhóm.

POST Booking trả Location tới Checkout GET và vẫn yêu cầu `X-Guest-Token`. VNPay GET và POST IPN đều dùng signed query string; form body không thay thế query. HTTP tests bao phủ các hợp đồng này.


## Phạm vi triển khai và phụ thuộc lịch sử 2026-10-13

| Phần | Trạng thái hiện tại | Còn cần ai / điều kiện nào |
| --- | --- | --- |
| Checkout one-way/round-trip, buyer/passenger, đổi/hủy ghế, token sở hữu | Đã implement + test | Không cần FE để test API; dữ liệu Trip/Boat/Seat/Fare phải hợp lệ |
| Session TTL, RedLock, SQL chống overlap/concurrency | Đã implement + test thật | Kiệt mapping cấu hình/quorum khi deploy; không sửa Docker/DevOps trong lượt TV2 |
| Payment URL, IPN HMAC, retry, callback muộn, Querydr | Đã implement + test local | Nhật Anh/merchant cung cấp VNPay credentials hợp lệ; Kiệt cung cấp IPN HTTPS công khai khi thử bank |
| Refund allocation, idempotency/budget, manual evidence, reconciliation | Đã implement + test local | Merchant phải bật refund và cung cấp kết quả bank thật; Admin cần trạng thái vận hành/tiến độ Trip đúng từ module vận hành |
| QR tĩnh per-Booking RS256 + public PEM | Đã implement + verify RSA thật | Tuấn tích hợp JWS/kid/version/public key vào preload + verify offline + check-in |
| UI đặt vé/manage order/checkout/QR | Chưa triển khai trong TV2 | Viễn nối các API mới, token và countdown; repo FE hiện chưa có luồng này |
| UI ghế đổi màu realtime | TV2 đã gọi ISeatEventPublisher sau commit | Kiệt phụ trách Hub/transport; Viễn nối UI và resync REST |
| Danh mục ghế/seat map/giá/tuyến, lịch và trạng thái chuyến | Dùng entity/service/seed hiện có; không sửa TV1 | Vương hoàn thiện API danh mục/inventory. Hiện chưa có endpoint discovery SeatId/seat map để người test tự chọn toàn bộ ghế qua REST |
| IAM/login kiến trúc, quyền Staff/Captain | Không sửa | Vương |
| AI/chatbot/audio, weather/tracking | Không thuộc TV2 | Thịnh/Kiệt và FE; không cần để test checkout/payment/refund/QR API |

Không gọi bank thật trong kiểm thử này. Gateway giả local kiểm tra HMAC request refund, ký HMAC response và trả kết quả giả; không có tiền thật chuyển đi. Không coi đây là nghiệm thu VNPay sandbox/production hoặc nghiệm thu toàn bộ UI/mobile.

## Môi trường demo lịch sử 2026-10-13 (cần kiểm tra lại trước khi dùng)

API: **http://127.0.0.1:5189**. Swagger: **http://127.0.0.1:5189/swagger**. API dùng DB demo riêng `WaterbusDomainTests_Smoke_*`, SQL host 14335; Redis ở 63795/63796/63797; merchant giả TESTCODE và gateway giả ở 5190. Không dùng/ghi dữ liệu DB phát triển của bạn. QR private key local được lưu trong User Secrets, không nằm trong repo.

Demo từng được để chạy sau kiểm thử ngày 2026-10-13; trạng thái hiện tại chưa được xác nhận. Nó cần Docker Desktop và process API còn chạy; đây không phải deployment thường trực. SQL/Redis dùng các container `waterbus_nhatanh_test_*`. Nếu Docker/app tắt, xem mục chạy lại bên dưới; không dùng collection này với merchant thật.

### Postman

1. Import `waterbus-be/tests/Manual/NhatAnh.postman_collection.json`.
2. Chạy folder **1 - Before payment** theo thứ tự. Collection có sẵn TripId/SeatId/bến của chuyến demo mới; hold trả token rồi collection tự lưu OrderId/BookingId/token ở runtime.
3. Kết quả mong đợi: hold 201 Draft; GET có token 200; thiếu token 403; QR chưa paid 409; submit 200; tạo/retry payment URL 200 và cùng merchant reference/deadline. Token không chứa trong QR.
4. Copy orderId và guestToken từ collection variables để chạy script bên dưới. Script chỉ nhận API loopback port 5189, chỉ ký merchant/hash giả TESTCODE; không đọc VNPay secret thật.

```powershell
cd D:\FPT_Study\Semester_8\PRN232\smart-waterbus\waterbus-system
& .\waterbus-be\tests\Manual\Simulate-LocalVnPayIpn.ps1 `
  -OrderId '<orderId vừa tạo>' -GuestToken '<accessToken của Order>'
```

5. Chạy folder **2 - After local IPN script**: Order Confirmed, có một Ticket, QR RS256 và public PEM.
6. Folder **3** là luồng thay thế hủy checkout **trước IPN**, không chạy sau khi đã thanh toán. Sau cancel, tạo hold mới để test lại. Chọn lại ghế đã bán trên cùng đoạn phải nhận 409.

Không export/commit token runtime. Collection có GUID của fixture demo phiên này; khi đổi DB hoặc ghế đã bán, thay tripId/seatId và bến bằng dữ liệu mới. Discovery SeatId/seat map qua REST còn phụ thuộc TV1; tạm thời lấy từ DB test hoặc fixture. Chuyến demo đóng bán sau khoảng 12 giờ; không dùng các GUID này như dữ liệu production.

### Swagger

`POST /api/v1/Checkouts/holds` với body legs như collection, copy accessToken/OrderId/BookingId. Các GET/submit/update/cancel/payment/QR hiện có ô **X-Guest-Token** trong Swagger; nhập token đó. Token body không thay thế header. Với refund, đăng nhập Admin qua module IAM và dùng nút Authorize với Bearer JWT. Refund Scheduled Trip phải bị từ chối; cần Trip Cancelled/Terminated hoặc receipt muộn đủ điều kiện. TV2 không thêm endpoint tự hủy Trip vì đó là module vận hành.

Có thể test đổi ghế với PUT seats bằng SeatId khác cùng Boat + danh sách passengers, trước payment attempt; expiresAt phải giữ nguyên. Sau payment attempt, đổi ghế phải 409. Chờ TTL rồi giữ lại ghế; worker dọn mỗi 30 giây nên inventory hiện có có thể nhả chậm khoảng một chu kỳ.

## Chạy lại BE / automated tests

Từ repo `waterbus-system`:

```powershell
dotnet build .\waterbus-be\WaterbusSystem.sln
dotnet test .\waterbus-be\WaterbusSystem.sln
```

Không cấu hình integration env thì SQL/Redis tests skip. Để kiểm tra đủ suite, đặt `WATERBUS_TEST_SQLSERVER` là SQL Server test, và `WATERBUS_TEST_REDIS` là ba connection Redis độc lập ngăn bằng `;`, rồi chạy dotnet test. Tests tự tạo/xóa DB `WaterbusDomainTests_*`; không dùng connection tới DB production.

Để chạy API thông thường, cấu hình SQL/Redis/JWT/VNPay/QR trong User Secrets hoặc env (xem `NHAT_ANH_COMMERCIAL_API.md`), bật Development nếu cần Swagger:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project .\waterbus-be\src\WaterbusSystem.WebApi --urls http://127.0.0.1:5189
```

Lệnh này dùng cấu hình thông thường của bạn, không tự bật merchant giả hoặc tạo DB demo. Không chạy script IPN demo vào API merchant thật. Không chuyển demo hash/JWT/credentials vào cấu hình production.

## Giới hạn cần biết khi nghiệm thu

- SQL lock hiện serialize toàn module commercial: đúng về concurrency trong các luồng TV2 đã test, nhưng throughput thấp hơn khóa theo inventory/Order. Writer từ module khác không được ghi commercial inventory bỏ qua cơ chế này.
- Cache/realtime sau commit là best effort; REST/DB là nguồn sự thật. Chưa thêm transactional outbox/schema để bảo đảm mọi event được gửi lại sau crash.
- Refund timeout không chứng minh thất bại. Khoản tự động còn Processing không cho manual chuyển lần hai; phải xác minh đúng khoản hoàn qua merchant/bank. Nếu query không cung cấp đủ định danh refund thì chưa thể tự chốt.
- Endpoint public key cấp key hiện tại; chiến lược preload/key rotation nhiều key cần thống nhất với Tuấn trước khi triển khai scanner.
- Mapping env TV2 mới vào Docker Compose thuộc Kiệt; việc thêm vào .env.example chưa tự làm container đọc các key đó.

Code còn local, chưa commit/push. Các thay đổi sẵn có ở `.gitignore` và `waterbus-be/docker-compose.yml` được giữ nguyên; không đưa chúng vào phạm vi sửa TV2.
