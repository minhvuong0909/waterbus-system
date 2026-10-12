# API Integration — Smart Waterbus FE ⇄ BE

Tài liệu này liệt kê **từng màn hình FE cần API nào**, BE (`waterbus-be`) hiện đã có gì, và **còn thiếu / lệch gì** để hai bên nối được với nhau.
Nội dung được đối chiếu trực tiếp với code BE (controllers, DTO, entities) và code FE (`src/features/*/services`, `types.ts`, `src/mocks`).

> **Hiện trạng:** FE đã có HTTP client (`src/services/apiClient.ts`), cấu hình qua `.env.local` (xem `.env.example`: `VITE_API_BASE_URL`, `VITE_USE_MOCK`).
> Đã nối API thật: **đăng nhập** (`POST /auth/login`, lưu JWT, đăng xuất, tự đăng xuất khi 401), **danh sách bến** (`GET /stations`), **tìm chuyến** (`GET /trips/search`, gọi lại khi đổi bến/ngày).
> Còn lại (BE chưa có endpoint) vẫn là mock trong `src/mocks/*`: đăng ký, Google, chi tiết chuyến, sơ đồ ghế, đặt vé, thanh toán, vé của tôi, tracking.
> Giai đoạn B (đặt vé): sơ đồ ghế theo bố cục tàu của BE (mock, `Seat`/`SeatMap`), sự kiện ghế realtime mô phỏng thay SignalR (`seatEvents.ts`), tạo booking + đếm ngược giữ ghế 600s (`createBooking` gọi `POST /bookings` khi seat id là Guid, hiện còn mock vì chưa có API sơ đồ ghế), trang `/payment/callback` (gọi thật `GET /payments/vnpay-return`), `/manage/:token` và chatbot đều là mock.
> Giai đoạn C (khu vận hành `/admin/*`, `/captain`): BE chưa có endpoint admin nào, nên toàn bộ chạy trên dữ liệu giả lưu trong bộ nhớ (`features/admin/services/adminService.ts`, mất khi tải lại trang). Chỉ danh sách bến lấy từ `GET /stations`. Mỗi hàm bám contract đã lên kế hoạch ở `BACKEND_TASK_BREAKDOWN.md` (P1-BE-01..04, P3-BE-01) để sau này thay bằng `apiRequest`. Phân quyền theo role nằm ở `features/admin/navigation.ts`.
> Đặt `VITE_USE_MOCK=true` để chạy toàn bộ bằng mock khi không có BE.

Ký hiệu: ✅ BE đã có · ⚠️ BE có nhưng lệch với FE · ❌ BE chưa có

---

## 1. Quy ước chung

| Mục | BE hiện tại | FE cần làm |
|---|---|---|
| Base URL | Prefix `api/v1/[controller]`. Cổng chạy chưa cố định (chưa có `launchSettings.json`) | Thêm `VITE_API_BASE_URL` (file `.env`), tạo HTTP client dùng chung |
| CORS | Whitelist `http://localhost:5173`, `http://localhost:3000` (`appsettings.Development.json`) | Giữ Vite chạy cổng **5173** |
| JSON | camelCase mặc định của ASP.NET | Kiểu TS khớp camelCase |
| Auth | JWT, claims: `sub`, `email`, `name`, `role`. Chưa có `[Authorize]` trên endpoint nào | Lưu token, gắn `Authorization: Bearer ...` khi có endpoint cần đăng nhập |
| Lỗi | `ProblemDetails` (RFC 7807): `title`, `detail`, `status`, `errors` (validation), `traceId`. **Riêng login** trả `{ message }` | Một hàm parse lỗi chung; riêng login đọc `message` |
| Mã lỗi | 400 validation, 404 not found, 409 conflict (ghế đã bị giữ/bán), 500 | Hiển thị 409 thành "ghế vừa bị người khác giữ" và quay lại chọn ghế |
| Ngôn ngữ | Header `Accept-Language`: chỉ hiểu `vi` (mặc định) và `en` | FE gửi `Accept-Language` theo ngôn ngữ đang chọn (FE đang có vi/en) |
| Id | Tất cả là `Guid` | FE đang dùng id chuỗi tự đặt (`bach-dang`, `trip-1`...) → phải chuyển sang Guid thật |
| Giờ | `DateTimeOffset` (ISO 8601) | FE đang dùng chuỗi hiển thị sẵn (`"07:30"`) → phải format từ ISO |
| Tiền | `decimal`, đơn vị VND | FE dùng `fareVnd: number`, ok |

---

## 2. Đối chiếu theo màn hình

### 2.1 Đăng nhập / Đăng ký — `/sign-in`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `signIn({email, password, remember})` | `POST /api/v1/auth/login` body `{email, password}` → `{userId, email, fullName, roles[], token}` | ⚠️ |
| `register({fullName, email, phone, password})` | — | ❌ (roadmap P1-BE-06) |
| `signInWithGoogle()` | — | ❌ (chưa có trong roadmap Phase 1) |
| `getAuthShowcase()` | — | Giữ tĩnh ở FE (nội dung marketing) |

Lệch cần xử lý:
- `AuthUser` của FE là `{id, fullName, email}` → map từ `userId`; thêm `roles`, `token`.
- FE có checkbox `remember` mà BE không có refresh token → chỉ quyết định lưu token ở `localStorage` hay `sessionStorage`.
- FE chưa có nơi lưu token / tự đăng xuất khi token hết hạn.
- Login lỗi trả `401 {message}`, khác định dạng ProblemDetails.
- Nút Google: ẩn hoặc giữ mock cho tới khi BE hỗ trợ.

### 2.2 Trang chủ — `/`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `getNextDepartures()` | Có thể dùng `GET /trips/search` (sắp xếp theo giờ, lấy vài chuyến đầu) | ⚠️ thiếu field `status`, `line`, `vesselCode` |
| `getCorridorMap()` (bến + vị trí tàu) | `GET /stations` có `latitude`, `longitude`, `orderIndex` | ⚠️ FE dùng toạ độ % trên ảnh bản đồ; cần quy đổi từ lat/lng hoặc giữ tĩnh. Vị trí tàu realtime: ❌ |
| `getHomeData()` (hero, stories, steps, filter) | — | Giữ nội dung tĩnh ở FE |

### 2.3 Tìm chuyến — `/search`, `/search/results`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `getPierOptions()` | `GET /api/v1/stations` → `{id, code, name, address, latitude, longitude, orderIndex, isActive}` | ⚠️ FE cần thêm `district`, `shortName`, `subtitle`. Chỉ có 5 bến (Bạch Đằng → Linh Đông) |
| `getNetworkRoutes()` | — (không có endpoint danh sách tuyến) | ❌ `RoutesController` chưa có (P1-BE-02) |
| `getDateOptions()` (dải ngày + giá từ + số chuyến) | — | ❌ Cần API tổng hợp theo ngày, hoặc FE tự tính từ kết quả tìm kiếm |
| `getTrips(query)` | `GET /api/v1/trips/search?departureStationId&arrivalStationId&departureDate&tripType` → `TripDto[]` | ⚠️ xem bảng dưới |

`TripDto` hiện tại: `id, routeName, departureStationName, arrivalStationName, departureTime, arrivalTime, durationMinutes, tripType, basePrice, availableSeatsCount`.

Field FE `Trip` mà BE chưa trả:

| FE field | Ghi chú |
|---|---|
| `vesselCode`, `vesselName` | Có trong entity `Boat` (`Code`, `Name`) nhưng không có trong DTO |
| `lineLabel`, `highlight`, `tagline`, `variant`, `band` | Nội dung hiển thị; `band` (sáng/chiều/hoàng hôn) FE tự suy ra từ giờ. `lineLabel`/`highlight`/`tagline` cần quyết định: BE trả hay FE tự ghép |
| `originGate`, `destinationGate` | ❌ chưa có khái niệm cổng/bến lên tàu |
| `direct`, `crossingKind`, `crossingCaption` | ❌ BE chưa có trạng thái "chạy thẳng / dừng bến" |
| `amenities[]` | ❌ chưa có |
| `seatsLow` | FE tự suy ra từ `availableSeatsCount` |
| `fareLabel` | FE tự format |

Lệch khác:
- `tripType` BE: `Commuter` / `Sightseeing`. FE: `transit` / `sightseeing` (và `TripType` của FE còn có nghĩa `one-way`/`round-trip` — trùng tên, cần đổi tên một bên). Khứ hồi: BE chưa hỗ trợ.
- FE có `passengers` (số khách) và lọc theo `ResultFilters` (khoảng giờ, loại tàu...). BE chưa có tham số số khách.
- **Hiện BE không có Trip nào được seed** → search luôn rỗng cho tới khi có engine sinh chuyến (P1-BE-04).

### 2.4 Chi tiết chuyến — `/trip`

| FE | BE | Trạng thái |
|---|---|---|
| `getTripDetail(tripId, query)` | — | ❌ `GetTripDetailQuery` chưa có (P1-BE-05) |

FE `TripDetail` cần thêm: nhãn bến đầy đủ + quận, `timeline[]`, `amenityDetails[]`, `boardingSteps[]`. Gợi ý: BE trả phần dữ liệu thật (bến, giờ, tàu, giá, ghế trống); `timeline`/`amenityDetails`/`boardingSteps` là nội dung tĩnh nên **giữ ở FE** (i18n), chỉ cần thêm một vài field vào DTO.

### 2.5 Chọn ghế — `/booking/seats`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `getSeatMap()` | — (không có endpoint sơ đồ ghế theo chuyến) | ❌ P1-BE-05 / P1-WEB-03 |
| `validateVoucher(code)` | — | ❌ chưa có voucher trong BE |

**Mô hình ghế lệch nhau** (cần chốt với leader trước khi code):

| | FE | BE |
|---|---|---|
| Phân loại | `standard` / `vip` (khoang cuối, phụ thu) | `FrontCabin` / `Standard` / `Outdoor` |
| Bố cục | Hàng `row` + cột `A–D`, cột A/D là ghế cửa sổ | `RowNumber` + `ColumnNumber` (số), `SeatCode` |
| Giá | Phụ thu VIP cộng thêm vào giá vé | `Seat.PriceMultiplier` |
| Trạng thái | `available` / `occupied` | Suy ra từ `Ticket` (Valid/CheckedIn/Pending) + Redis lock |
| Số ghế | Theo mock | 60 ghế / tàu, 3 khoang |

Cần quyết định: map `FrontCabin/Outdoor/Standard` ↔ `standard/vip` ra sao, hay sửa FE theo 3 khoang? Ghế "cửa sổ" là thuộc tính FE tự suy ra hay BE trả?
Ghế đang được giữ bởi người khác (Redis lock) cũng nên được tính là `occupied`; BE hiện chỉ biết ghế đã có vé.

### 2.6 Thông tin hành khách & xem lại — `/booking/passenger`, `/booking/review`

Không có API riêng; dữ liệu được gửi cùng lúc đặt chỗ (mục 2.7).

Lệch:

| FE `PassengerForm` | BE `CreateBookingCommand` |
|---|---|
| `category` (adult/senior/child), `dateOfBirth`, `assistance[]` | ❌ không có; vé chỉ có `PassengerName` (= tên người đặt) |
| `fullName`, `phone`, `email` | `customerName`, `customerPhone`, `customerEmail` |
| `phoneCode` + `phone` | BE regex `^[0-9]{10,11}$` — FE phải ghép/chuẩn hoá số điện thoại |
| Nhiều hành khách (mỗi ghế một người) | Một người đặt cho cả booking, tối đa 10 ghế |

Cần quyết định: BE có lưu thông tin từng hành khách và giá theo nhóm tuổi (người cao tuổi/trẻ em) không? Nếu không, FE bỏ các trường đó hoặc chỉ hiển thị.

Giá: BE tính `BasePrice × số ghế` và **bỏ qua `PriceMultiplier`**, nên phụ thu VIP và voucher giảm giá của FE sẽ không khớp tổng tiền BE trả về. Cần BE tính giá theo từng ghế (và voucher nếu giữ).

### 2.7 Đặt chỗ — kết thúc bước review

| FE | BE | Trạng thái |
|---|---|---|
| Hiện tạo "booking" trong Redux (`bookingSlice`) | `POST /api/v1/bookings` body `{tripId, seatIds[], customerName, customerEmail, customerPhone}` → `201 {bookingId, bookingCode, totalAmount, status, holdDurationSeconds}` | ⚠️ |

Cần ở FE:
- Gửi `seatIds` là Guid thật (từ sơ đồ ghế API).
- Dùng `holdDurationSeconds` (600) để hiện đồng hồ đếm ngược giữ chỗ; hết giờ thì báo và quay về chọn ghế.
- `409` → báo ghế đã bị giữ, tải lại sơ đồ ghế.
- `400` + `errors` → hiển thị lỗi theo từng trường.
- Lưu `bookingId`, `bookingCode`, `totalAmount` (thay cho `BookedSnapshot` hiện tại).

Phía BE còn thiếu: huỷ booking `Pending` quá hạn (P1-BE-07), sửa lock-leak (P1-BE-08).

### 2.8 Thanh toán — `/payment`, `/payment/success`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `getPaymentMethods()` (QR / thẻ / ví) | — | ⚠️ BE **chỉ có VNPAY** |
| `getEwallets()` | — | ❌ (MoMo thuộc backlog) |
| `confirmPayment()` | `POST /api/v1/payments/create-vnpay-url` body `{bookingId}` → `{paymentUrl, bookingCode}` | ⚠️ |
| — | `GET /api/v1/payments/vnpay-return?...` → `{success, bookingCode, responseCode, message}` | ✅ (chưa có FE) |
| — | `GET /api/v1/payments/vnpay-ipn` (VNPAY gọi ngầm, FE không gọi) | ✅ |

Luồng cần dựng ở FE:
1. Bấm thanh toán → gọi `create-vnpay-url` → `window.location.href = paymentUrl`.
2. VNPAY redirect về **`http://localhost:5173/payment/callback?...`** (`VnPay:ReturnUrl` trong `appsettings.json`). **FE chưa có route này** → thêm `/payment/callback`: đọc nguyên query string, gọi `GET /payments/vnpay-return` với đúng query đó, rồi chuyển sang `/payment/success` hoặc trang thất bại.
3. Trạng thái thật do IPN cập nhật; trang callback chỉ hiển thị kết quả.

Cần quyết định: giữ các phương thức QR/thẻ/ví như giao diện minh hoạ, hay rút gọn chỉ còn VNPAY (khuyến nghị)? Chưa có trang "thanh toán thất bại".

### 2.9 Vé của tôi — `/tickets`, `/tickets/detail`, `/tickets/manage`

| FE (service) | BE | Trạng thái |
|---|---|---|
| `getTicketWallet()` (sắp tới / sau / đã đi) | — | ❌ chưa có "vé của người dùng" (cần `ICurrentUserService` — P1-BE-06) |
| `getTicket(id)` (vé + mã QR) | — | ❌ Ticket có `TicketCode`, `QrSeed` nhưng chưa có endpoint |
| `getManageBooking()` / tra cứu mã đặt vé | — | ❌ `GetBookingByCodeQuery` (P1-BE-05) |
| `requestRefund()` | — | ❌ chưa có luồng hoàn/huỷ vé |
| `askAssistant()` (chat) | — | ❌ AI Assistant (Semantic Kernel) thuộc Phase 4 |
| `getBookedTickets()` (vé vừa đặt trong phiên) | — | Hiện lưu trong Redux; thay bằng API tra cứu |

Lệch: `Booking` hiện không gắn với `UserId` nên chưa lọc được "vé của tôi". Hỗ trợ khách vãng lai tra cứu bằng mã đặt vé + email/SĐT (FE có `GuestLookupStrip`).

### 2.10 Theo dõi chuyến — `/tracking`

| FE | BE | Trạng thái |
|---|---|---|
| `getTrackingData()` (vị trí tàu, POI, thuyết minh audio) | — | ❌ Thuộc Phase 2+ (GPS realtime, audio guide). Giữ mock |

### 2.11 Khám phá, Trợ giúp, Sơ đồ trang

`/explore`, `/help`, `/sitemap` là nội dung tĩnh → không cần API ở Phase 1. `getExploreData()` giữ mock.

---

## 3. Tóm tắt: BE cần bổ sung (đề xuất, chờ leader chốt)

Theo thứ tự FE cần:

1. **Seed Trip** + engine sinh chuyến theo lịch (P1-BE-04) — nếu không, tìm chuyến luôn rỗng.
2. **`TripDto` mở rộng**: `boatCode`, `boatName`, `departureStationId`, `arrivalStationId`, (tuỳ) `gate`, `amenities`.
3. **`GET /trips/{id}`** (chi tiết chuyến) và **sơ đồ ghế theo chuyến**: mỗi ghế gồm `id`, `seatCode`, `category`, `row`, `column`, `priceMultiplier`/giá, `status` (gồm cả ghế đang bị Redis lock).
4. **Tính giá theo ghế** trong `CreateBooking` (dùng `PriceMultiplier`), và quyết định voucher.
5. **`GET /bookings/{code}`** (tra cứu) và **vé của tôi** (gắn `Booking` với user, `ICurrentUserService`).
6. **Register** + thêm `[Authorize]` cho các endpoint cần đăng nhập.
7. **Quyết định thông tin hành khách** (nhóm tuổi, ngày sinh, hỗ trợ) có lưu không.
8. **Cổng chạy cố định** (`launchSettings.json`) và `ReturnUrl` VNPAY khớp route FE.

## 4. Thứ tự nối phía FE (đề xuất nhánh)

Mỗi bước là một nhánh/PR nhỏ, chỉ sau khi BE có endpoint tương ứng:

| # | Nhánh | Nội dung | Phụ thuộc BE |
|---|---|---|---|
| 1 | `feature/fe-api-client` ✅ | `.env`, HTTP client, gắn token, parse lỗi ProblemDetails, `Accept-Language` | Không |
| 2 | `feature/fe-auth-login` ✅ | Nối `signIn`, lưu token, đăng xuất, `PrivateRoute` theo role | Đã có |
| 3 | `feature/fe-stations-trips` ✅ | Nối bến + tìm chuyến, đổi id sang Guid, map `TripDto` → `Trip` (chỉ có dữ liệu khi BE đã có Trip) | Cần seed Trip |
| 4 | `feature/fe-trip-seats` | Chi tiết chuyến + sơ đồ ghế thật | P1-BE-05 |
| 5 | `feature/fe-booking-vnpay` | Tạo booking, đếm ngược giữ ghế, VNPAY, route `/payment/callback` | Đã có (cần sửa giá) |
| 6 | `feature/fe-my-tickets` | Tra cứu / vé của tôi | P1-BE-05, P1-BE-06 |

Các trang tracking, explore, help, home giữ mock cho tới các phase sau.
