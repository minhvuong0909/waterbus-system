# Tích hợp API web và Staff Scanner

## Phạm vi

Đối chiếu toàn bộ controller HTTP, DTO/query/command liên quan, cấu hình auth, SignalR,
thanh toán, QR và luồng scanner. Backend hiện cung cấp **48 thao tác HTTP trên 42 path**.
Nguồn hợp đồng chạy thật: `/swagger/v1/swagger.json` trong Development.

Nhánh tích hợp lấy client mobile từ `origin/main` (`8f2c284`) và implementation check-in
có sẵn từ `feature/checkin-backend-workflow` (`79b8d36`). Các thay đổi backend ngoài module
check-in gồm DI, DbContext/interface, Hangfire và cấu hình mẫu; không sửa migration/entity.

## Màn hình và endpoint

| Nhóm API | Client / cách test |
| --- | --- |
| Auth login | Web `/sign-in`; mobile `/login`. Staff chỉ dùng Scanner; không có Dispatcher/Accountant. |
| Stations list/paged/detail/create/update | Trang Admin Stations dùng dữ liệu thật, tạo/cập nhật bằng API. Ngừng hoạt động dùng `PUT isActive=false`, không xóa vật lý. Paged/detail có trong Test API. |
| Boats list/detail/create/update | Admin Boats lấy toàn bộ trang, tạo tàu và ngừng hoạt động bằng API. Backend chỉ nhận tổng số ghế, chưa hỗ trợ cấu hình sơ đồ ghế qua API. Các trường cập nhật khác và CaptainUserId có trong Test API. |
| Trips search/detail/seats | Web tìm chuyến, chi tiết và chọn ghế gọi REST thật. Giá và availability theo đoạn TripStopCall. |
| Trips operations/events/status/SOS | Admin Overview/Trips/Incidents và Captain Portal dùng endpoint thật; các endpoint cũng có trong Test API. |
| Tracking snapshot/history/positions | Admin radar dùng snapshot + hub; Captain gửi GPS qua hub. Lịch sử và gửi vị trí REST dự phòng có form Test API, backend vẫn kiểm đúng role. |
| Incidents list/detail/weather-check | Admin Incidents / radar + Test API. Quyết định trạng thái qua Trips status, không tự đổi trạng thái từ Incident. |
| Checkouts create/hold/get/submit/cancel/update-seats/QR/public-key | Luồng đặt vé web dùng hold → submit → payment → GET order → QR. Create trực tiếp và đổi ghế có trong Test API. Token Guest gửi bằng `X-Guest-Token`; JWT có thể dùng không cần guest token, không bị coi là mock. |
| Bookings POST | Endpoint tương thích có trong Test API; luồng web chính dùng Checkouts. |
| Payments URL/return/reconcile | Web chuyển hướng VNPay, kiểm callback và đọc lại trạng thái server; reconcile có trong luồng thanh toán và Test API. |
| Payments IPN GET/POST | Chỉ dành cho gateway, không có nút tự tạo bằng chứng thanh toán. Return đi qua luồng callback thực tế. |
| Refund request/manual-confirm/reconcile | Form Test API dành cho Admin, body dựng từ hợp đồng Swagger. Không thêm nút refund tự phục vụ cho Passenger. |
| CheckIns submit/sync/manifest/assignments-mine | Mobile đăng nhập Staff → lấy phân công → tải manifest + RSA public key → xác thực QR offline → lưu hàng đợi → sync. Mobile mặc định ghi offline theo lô; submit đơn có trong Test API để kiểm hợp đồng, vẫn yêu cầu Staff. |
| CheckIns window/assign/revoke | Admin Test API có form tham số và JSON. Cần DeviceId đã tồn tại; backend chưa có API tạo ScannerDevice. |
| AiAssistant chat | Widget gọi API thật và hiển thị lời đáp + trip cards được server trả về. Card có `boardingStationId`/`disembarkingStationId` để nút **Xem chuyến** mở trang chi tiết/chọn ghế của đúng đoạn; không tự bịa URL. Nếu AI chưa được cấu hình, hiển thị lỗi thay vì câu trả lời mock. |

### AI Trip Card trong Development

`POST /api/v1/AiAssistant/chat` trả `cards[]` từ tool tìm chuyến, không bóc chuyến từ lời văn của model. Ngoài các trường card cũ, mỗi card có `boardingStationId`, `disembarkingStationId` (GUID bến khách hỏi) và `isDevelopmentPreview`. FE dùng `tripId` cùng hai ID bến để mở `/trip`; trang này tải lại trip detail và seat map từ API trước khi đặt vé. `bookingUrl` chỉ có nếu tool tạo link chính thức đã được cấu hình.

Widget gửi `{ "message": "Ngày 2026-10-20", "history": [{ "role": "user", "content": "Tìm chuyến từ Bến Bạch Đằng đến Bến Linh Đông" }, { "role": "assistant", "content": "Quý khách muốn đi ngày nào?" }] }` để AI hiểu câu bổ sung. `history` là tối đa 8 lượt trước tin hiện tại (`user`/`assistant`, tối đa 1000 ký tự mỗi lượt); backend không lưu phiên hội thoại hay tin nhắn trong DB. Client không gửi lời chào mặc định. AI chỉ dùng câu chữ model để hỏi thêm thông tin; giờ/giá/link và `cards[]` vẫn chỉ từ tool. Nếu Groq lỗi hoặc bị giới hạn 429 **sau khi** tool đã chạy thành công, backend giữ kết quả tool để hiển thị card hoặc báo không có chuyến, không hiển thị câu chữ model lỗi. Lỗi trước khi tool chạy vẫn là fallback.

Widget AI luôn gọi endpoint này, kể cả khi `VITE_USE_MOCK=true` cho các module khác; không còn câu trả lời canned, Trip Card mẫu hay nút gợi ý Refund/Trip Status không có tool phía backend. Khi backend/AI chưa khả dụng, widget hiển thị lỗi kết nối thay vì dữ liệu giả.

AI tool `SearchTrips` nhận bến và ngày dưới dạng bộ lọc tùy chọn: không nêu bến thì liệt kê các chuyến sắp tới; nêu một bến như "Linh Đông có chuyến nào?" thì tìm cả chuyến xuất phát **và** chuyến đến bến đó; nêu đủ hai bến thì giữ đúng chiều đi. Adapter chấp nhận tên bến có/không có tiền tố "Bến" khi khớp duy nhất. Kết quả đến trực tiếp từ `SearchTripsQuery`, loại chuyến đã qua giờ khởi hành và trả tối đa 10 card sớm nhất. Ngày không hợp lệ hoặc bến không xác định được thì assistant hỏi lại thay vì bịa dữ liệu. Tool `ListStations` lấy danh sách bến đang hoạt động từ `GetStationsQuery` khi khách hỏi có những bến nào. API Trip Search hiện chỉ lọc theo bến đầu/cuối của route; không được trình bày tìm kiếm một bến trung gian như đã hỗ trợ. Giá/ghế/bến trong card vẫn chịu các giới hạn Trip Search nêu dưới đây.

AI chat hiện có thể nhận bộ lọc giờ theo giờ Việt Nam (`earliestDepartureTime`, `latestDepartureTime`, `preferredDepartureTime`, `latestArrivalTime`, dạng `HH:mm`) và `passengerCount`; giờ mong muốn xếp tối đa 3 chuyến gần nhất lên đầu. Response chat có `searchContext` để FE gửi lại trong request kế tiếp; ví dụ "sớm hơn", "đổi ngày", "thêm một người" chỉ sửa tiêu chí liên quan. Ngữ cảnh là bộ lọc do client gửi lại, không phải dữ liệu đặt vé được server lưu. Nếu nhập khu vực không trùng tên bến, adapter chỉ gợi ý bến từ tên/địa chỉ của `GetStationsQuery`, không tự đoán một bến. Chọn card trong widget (nút hoặc "Chọn chuyến A") sẽ xác nhận lựa chọn rồi chuyển `tripId`, bến đi/đến, ngày và nhãn số khách tới luồng chi tiết chuyến hiện có; **chọn chuyến không tạo Booking**, việc giữ ghế/kiểm tra giá vẫn thuộc luồng đặt vé. Dữ liệu giá và ghế trên card chỉ là dữ liệu API Trip Search hiện trả về.

Trip Search hiện còn trả `basePrice=0` và bến theo thứ tự cũ. Để thử luồng UI nội bộ, chỉ trong môi trường **Development** có thể bật `AiBookingAssistant:Integration:EnableUnverifiedTripCardsForDevelopment=true` (mặc định `false`). Khi đó card giữ `0 ₫`, `isPriceVerified=false`, `isDevelopmentPreview=true`; đây không phải giá vé đã xác nhận. Cờ này không có tác dụng ngoài Development và **không** thay thế việc owner Trip Search sửa/xác nhận giá, bến và availability theo đoạn. Không bật `IsTripSearchContractVerified` để vượt qua gate kiểm chứng.

Test API tại `/admin/api` chỉ xuất hiện khi chạy Vite Development và chỉ mở cho Admin.
Danh sách, tham số và JSON mẫu được đọc trực tiếp từ Swagger. Backend vẫn là nguồn quyết
định phân quyền; Admin không thể gọi thay quyền Staff/Captain chỉ nhờ màn hình này.

## Cấu hình kết nối

- Web mặc định gọi `/api/...`, `/hubs/...` cùng origin. Vite proxy tới `127.0.0.1:5000`;
  nginx dùng proxy tương ứng. Chỉ đặt `VITE_API_BASE_URL` khi triển khai khác origin.
- Mobile: đặt `EXPO_PUBLIC_API_BASE_URL` thành URL HTTPS khi deploy. Để trống lúc dev thì
  lấy hostname của trình duyệt hoặc IP Metro LAN, API port 5000. Điện thoại cùng Wi-Fi;
  không dùng `localhost` để trỏ về máy tính.
- CORS backend cần cho phép origin mobile web thực tế. API/native và Metro cần truy cập
  được qua LAN; truy cập bằng điện thoại chưa được kiểm chứng trên thiết bị vật lý.
- JWT native lưu SecureStore; mobile web dùng bộ nhớ, cần đăng nhập lại sau reload.
- `Qr:PrivateKeyPem` phải có RSA >=2048-bit trong User Secrets/biến môi trường. Public key
  chỉ tải trước, không gọi server cho mỗi lượt quét. Không lưu private key trong repo.
- API lỗi giữ nguyên ProblemDetails, web/mobile gửi `Accept-Language`.

## Kiểm tra luồng thật

1. Đăng nhập Admin bằng tài khoản demo được seed bởi `ApplicationDbContextInitializer`.
2. Mở Stations/Boats để kiểm dữ liệu đã lưu; `/admin/api` để test các endpoint chưa có màn riêng.
3. Tìm chuyến từ Bạch Đằng đến Linh Đông. Database UI local hiện có chuyến ngày **19/10/2026**.
4. Chọn ghế → nhập khách → submit. Đơn `PendingPayment` chưa được phát Ticket/QR.
5. Chuyển VNPay và xác nhận thanh toán qua IPN thật. Không dùng `mock=1` trên callback để
   giả xác nhận. Hoàn tiền cần payment đã thành công và Booking đủ điều kiện.
6. Mobile đăng nhập Staff → Tải Vé → chọn phân công → preload. Sau khi có vé đã phát,
   quét QR Booking; trên mobile web có thể dán JWS vào **Nhập Mã** để test xác thực.
7. Chỉ check-in đúng Trip/boarding point, cửa sổ giờ, phân công Primary Offline và Ticket hợp lệ.
   Sync lỗi giữ Pending; server từ chối nghiệp vụ được ghi Conflict, không báo thành công.

Database local dành riêng cho lần test này là `WaterbusUiDev` trên SQL `127.0.0.1:11433`,
Redis `127.0.0.1:16379`. Script `waterbus-be/tests/infrastructure/seed-ui-scanner.sql` chỉ tạo
thiết bị test trong đúng DB này; phân công/cửa sổ được tạo qua API Admin. Không seed Ticket
hoặc xác nhận payment giả để làm UI trông như đã thanh toán.

Đã smoke test: Admin/Staff login; quyền Staff; public key RS256; manifest; giữ ghế Draft;
submit PendingPayment không phát vé; tạo URL VNPay; hủy đơn và nhả ghế; web gọi API qua
proxy; CORS mobile LAN. Chưa thực hiện giao dịch VNPay end-to-end hoặc quét camera trên
điện thoại vật lý. Manifest của fixture ban đầu chưa có vé thanh toán.

## Chưa có API để nối

- Đăng ký, Google login, quên mật khẩu.
- CRUD Route/Schedule, sinh chuyến, đổi tàu Trip, bảng giá, cấu hình sơ đồ ghế.
- Lịch sử vé toàn bộ Passenger/tài khoản và tra cứu Booking tùy ý chỉ bằng mã. Ví hiện chỉ
  hiển thị Ticket thật của đơn trong phiên, hoặc đơn có `orderId` + quyền truy cập hợp lệ.
- Passenger/Guest REST snapshot vị trí chuyến, POI/nội dung audio, tạo/phát audio qua HTTP.
  Radar Admin có API riêng, không mở endpoint Admin cho Passenger.
- Audio pipeline/TTS/storage/trigger hiện là service nội bộ, chưa có controller. Trang
  `audio-preview.html` vẫn là demo đã ghi nhãn, không được coi là tích hợp Azure/API audio.

Nội dung giới thiệu và bài viết tĩnh vẫn là nội dung giao diện. Mock nghiệp vụ chỉ được bật
bằng `VITE_USE_MOCK=true`; không tự fallback sang thành công giả khi API lỗi.

## Kết quả kiểm tra

- Backend build pass; 588 unit tests pass, 15 test hạ tầng skip do không đặt biến môi trường riêng cho suite.
- Web build pass; 73 tests pass.
- Mobile TypeScript pass; 19 kiểm tra scanner pass, có cả lỗi mạng và ràng buộc phân công/cửa sổ giờ.
- Bundle Android export pass với `--no-bytecode`; đây là kiểm tra bundle JavaScript/native imports,
  chưa phải APK hay xác minh camera trên thiết bị thật.
- UI thật: Admin đăng nhập/gọi Stations, Staff đăng nhập/lấy phân công/preload manifest + RSA public key thành công.
