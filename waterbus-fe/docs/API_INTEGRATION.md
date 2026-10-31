# API Integration — Smart Waterbus FE ⇄ BE

Tài liệu này liệt kê **từng màn hình FE cần API nào**, BE (`waterbus-be`) hiện đã có gì, và **còn thiếu / lệch gì** để hai bên nối được với nhau.
Nội dung được đối chiếu trực tiếp với code BE (controllers, DTO, entities) và code FE (`src/features/*/services`, `types.ts`).

> **Hiện trạng (sau khi gỡ mock):** FE không còn chế độ mock (`VITE_USE_MOCK`, `src/mocks` đã xoá); mọi màn gọi BE thật qua `src/services/apiClient.ts` (`VITE_API_BASE_URL`). Nội dung biên tập không có API (hero/câu chuyện trang chủ, Explore, giới thiệu màn đăng nhập, tiện ích/các bước lên tàu) nằm ở `src/content/`.
> Đã nối: đăng nhập, bến, tìm chuyến, chi tiết chuyến, sơ đồ ghế, giữ ghế + submit + VNPay + QR (Checkout), vé theo đơn (`GET /checkouts/{id}`), vận hành chuyến/sự cố/thời tiết/GPS (Admin, Captain), TTS và **Audio guides** (`GET /audioguides/pois`, `POST /audioguides/publish`), trợ lý AI, bến/tàu (CRUD Admin).
> BE chưa có endpoint nên FE báo 501/giữ trạng thái đọc: đăng ký, đăng nhập Google, theo dõi chuyến cho Passenger/Guest, bảng giá Admin (chỉ `GET /fares` đọc), lịch chạy/sinh chuyến, đổi tàu cho chuyến, danh sách tuyến.
> BE đã có nhưng FE chưa có giao diện: hoàn tiền Admin (`/refunds/*`), cửa sổ check-in và phân công Staff (`/checkins/windows`, `/checkins/assignments*`), lịch sử GPS (`GET /tracking/boats/{id}/history`), tạo đơn một lần (`POST /checkouts`), đổi ghế (`PUT /checkouts/{id}/bookings/{id}/seats`), `GET /stations/paged`. Staff Scanner (`/checkins`, `/sync`, `/manifest`) thuộc app mobile.

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

### 2.5 Chọn ghế thật — `/booking/seats`

| FE | BE | Trạng thái |
|---|---|---|
| `getSeatMap(tripId, originId, destId)` bước 1 | `GET /api/v1/trips/{tripId}` → `stops[]` (`callId`, `stationId`, `visitOrder`, `canBoard`, `canAlight`, `status`), `boat`, `canBook` | ✅ |
| bước 2 | `GET /api/v1/trips/{tripId}/seats?boardingCallId&disembarkingCallId` → `{canBook, salesState, boardingVisitOrder, disembarkingVisitOrder, seats[{seatId, seatCode, rowNumber, columnNumber, seatClassName, price, status, holdExpiresAt}]}` (công khai, no-store) | ✅ |
| `subscribeSeatEvents` | SignalR `/hubs/seat-sync` (`JoinTrip`; `seatHeld/seatReleased/seatSold`) | ✅ |

- Điểm lên/xuống lấy từ **stop thật** của chuyến (`pickJourney`): stop đầu tiên ở bến đi có `canBoard`, rồi stop ở bến đến có `canAlight` và `visitOrder` lớn hơn (bỏ stop `Skipped`). Không dùng `Station.OrderIndex`; chuyến vòng có hai lần ghé một bến vẫn đúng.
- Ghế: `seatId` là Guid; cabin suy từ `seatClassName` (Front/Outdoor/khác = Standard); cột `columnNumber` 1–4 → A–D; **giá lấy thẳng `price`** của từng ghế (giá phẳng theo loại chuyến × hạng ghế, BR-FARE) nên không còn phụ thu `PriceMultiplier`. Ghế `unavailable`/thiếu giá hiển thị là đã bán (không chọn được). `canBook=false` khoá nút Continue.
- Realtime theo **đoạn**: mỗi event có `boardingVisitOrder/disembarkingVisitOrder`; FE chỉ áp dụng khi `b < myDisembark && d > myBoard`. Sau reconnect FE tải lại seat map bằng REST (event lúc mất kết nối không được gửi bù).
- Chuyến mock (mock mode hoặc id không phải Guid): giữ sơ đồ mock cũ, đánh dấu rõ trong code (`isLiveTrip`).

### 2.6 Thông tin hành khách — `/booking/passenger`

Không có API riêng; dữ liệu đi cùng `POST /checkouts/{id}/submit` (mục 2.7). Theo BR v2.0:

| Đối tượng | Trường | Bắt buộc | Nguồn BR |
|---|---|---|---|
| Mỗi ghế (hành khách) | `passengers[i].name` | ✅ | BR-COM-06 |
| | `passengers[i].email`, `phone` | tuỳ chọn | BR-COM-06 |
| Người mua | `customerName`, `customerEmail` | ✅ | BR-COM-07 |
| | `customerPhone` | tuỳ chọn (10–11 chữ số nếu có) | BR-COM-07 |

FE: `PurchaserForm` + `passengers: Record<seatId, SeatPassengerForm>` trong `bookingSlice`; kiểm tra bằng `purchaserErrors` / `seatPassengerErrors` / `isBookingComplete`, nút Continue khoá tới khi đủ. Đã bỏ: hạng khách, ngày sinh, hỗ trợ lên tàu (không có trong BR v2.0).

### 2.7 Giữ ghế rồi gửi người mua (`useSeatHold`, `useSubmitCheckout`)

Luồng đúng BR-HOLD: **giữ ghế trước, nhập người mua sau**.

| Bước | FE | BE |
|---|---|---|
| Ở trang chọn ghế, bấm Continue | `createHold` | `POST /api/v1/checkouts/holds` body `{legs:[{tripId, seatIds[], boardingStationId, disembarkingStationId, boardingCallId, disembarkingCallId}]}` → `201 {orderId, quotedTotal, status:"Draft", expiresAt, accessToken, bookings[]}` |
| Ở trang review, bấm Continue to Payment | `submitCheckout` | `POST /api/v1/checkouts/{orderId}/submit` + `X-Guest-Token`, body `{customerName, customerEmail, customerPhone?, passengers:[{bookingId, seatId, name, email?, phone?}]}` → Draft → PendingPayment |
| Đổi ghế sau khi đã giữ | `cancelCheckout` rồi giữ lại | `DELETE /api/v1/checkouts/{orderId}` |

- **Hạn giữ thật:** `expiresAt` của backend → `hold.expiresAt` → `HoldCountdown` (hiện ở trang ghế, hành khách, review, thanh toán). Hết hạn → báo và quay lại chọn ghế.
- **Guest token:** `accessToken` chỉ trả **một lần** ở response giữ ghế → lưu trong `booking.hold`/`booking.order` (Redux, nhân bản sang `sessionStorage` để sống qua lần chuyển sang VNPay và quay lại). Mọi lời gọi sau gửi `X-Guest-Token` (`apiRequest({ headers })`).
- `409` → ghế vừa bị giữ/bán → chọn ghế khác; `400` + `errors` → báo lỗi.
- Chưa dùng `PUT /checkouts/{id}/bookings/{id}/seats` (đổi ghế tại chỗ) và `POST /checkouts` (tạo đủ một lần); FE hiện luôn đi đường holds → submit. `legs` hiện 1 chuyến, chưa có UI khứ hồi.
- Chưa có Ticket ở bước này: vé chỉ phát sau khi thanh toán được xác nhận (BR-COM-08).

### 2.8 Thanh toán, vé và QR — `/payment`, `/payment/callback`, `/payment/success`

| FE | BE | Trạng thái |
|---|---|---|
| `createPaymentUrl(orderId)` | `POST /api/v1/payments/create-vnpay-url` body `{orderId}` + `X-Guest-Token` → `{paymentUrl, merchantReference, amount, expiresAt}` | ✅ |
| `verifyVnPayReturn(search)` | `GET /api/v1/payments/vnpay-return?...` → `{success, merchantReference, responseCode, message}` (chỉ kiểm chữ ký chuyển hướng) | ✅ |
| `useOrderConfirmation` (poll) | `GET /api/v1/checkouts/{orderId}` → `status`, `bookings[].tickets[]` | ✅ |
| `reconcilePayment` | `POST /api/v1/payments/{orderId}/attempts/{paymentId}/reconcile` | ✅ |
| `getBookingQr` | `GET /api/v1/checkouts/{orderId}/bookings/{bookingId}/qr` → `{payload (JWS RS256), algorithm, keyId, credentialVersion}` | ✅ |
| — | `GET/POST /api/v1/payments/vnpay-ipn` (VNPAY gọi ngầm, FE không gọi) | ✅ |
| (chưa dùng) | `GET /api/v1/checkouts/qr-public-key` — dành cho Staff Scanner | ✅ |

Luồng (BR-PAY-01: chỉ IPN đã xác minh mới là bằng chứng thanh toán):
1. `/payment`: nút **Pay with VNPay** → `create-vnpay-url` → `window.location.assign(paymentUrl)`. Không còn nút "I've Completed Payment"; panel QR/thẻ/ví giả đã gỡ, chỉ còn VNPay.
2. VNPAY chuyển về `/payment/callback?...`. Trang gọi `vnpay-return` rồi **poll `GET /checkouts/{orderId}` mỗi 3 giây (tối đa ~2 phút)** tới khi `status` là `Confirmed` → `/payment/success`; `Expired`/`Cancelled` → báo ghế đã nhả; hết thời gian chờ → **Check again** / **Verify with the bank** (`reconcile`).
3. `/payment/success` chỉ hiển thị khi `Confirmed`: tên hành khách lấy từ `tickets[].passengerName`, mã đặt chỗ từ `bookingCode`, và **QR thật** = `payload` của `GET …/qr` (component `QrCode` dùng thư viện `qrcode`, quét được; Staff Scanner verify offline bằng public key).

### 2.8a Luồng Checkout

- Chuyến và hai bến phải là Guid từ API tìm chuyến (`isLiveTrip`); nếu không FE từ chối (400) thay vì giả lập: trip detail, seat map, holds, submit, create-vnpay-url, poll, QR đều gọi backend thật.
- **Chưa nghiệm thu E2E với VNPay sandbox** (cần BE chạy với `VnPay__*`/`Qr__PrivateKeyPem` và URL IPN công khai); đã kiểm bằng unit test (mock `fetch`) và chạy thử luồng mock trên trình duyệt.
- Còn lệch phía tìm chuyến: `TripDto.basePrice` hiện là 0 (placeholder) nên giá ở danh sách chuyến chưa đúng; giá chuẩn lấy từ seat map (xem `docs/KNOWN_ISSUES.md`).

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
