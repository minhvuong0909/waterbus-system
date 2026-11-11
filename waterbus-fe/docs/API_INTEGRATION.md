# Tích hợp API (waterbus-fe ↔ waterbus-be)

Mọi lời gọi đi qua `src/services/apiClient.ts` (gốc `VITE_API_BASE_URL`, header `Accept-Language`,
`Authorization: Bearer` khi đã đăng nhập, lỗi ProblemDetails → `ApiError`). Đường dẫn khai báo ở
`src/constant/apiEndpoints.ts`; FE không có dữ liệu mock. Nội dung biên tập không có API (FAQ, quy tắc vận
chuyển, giới thiệu bến, POI của trang Khám phá, các bước lên tàu) nằm ở `src/content/`.

## Endpoint FE đang dùng

| Endpoint | Service | Màn hình |
|---|---|---|
| `POST /auth/login` | `authentication/services/authenticationService` | Đăng nhập |
| `GET /stations`, `GET /fares` | `trips/services/tripService`, `fareService` | Form tìm chuyến, trang chủ, giá vé |
| `GET /trips/search` | `tripService.getTrips` | Kết quả tìm, chuyến sắp chạy (trang chủ, Khám phá) |
| `GET /trips/{id}`, `GET /trips/{id}/seats` | `tripService.getTripDetail`, `booking/services/bookingService` | Chi tiết chuyến, chọn ghế |
| `POST /checkouts/holds`, `POST /checkouts/{id}/submit`, `GET /checkouts/{id}`, `DELETE /checkouts/{id}` | `bookingService` | Giữ ghế, gửi người mua, trạng thái đơn, huỷ đơn |
| `GET /checkouts/{id}/bookings/{bookingId}/qr` | `bookingService.getBookingQr` | Vé, trang đặt vé thành công |
| `POST /payments/create-vnpay-url`, `GET /payments/vnpay-return`, `POST /payments/{orderId}/attempts/{paymentId}/reconcile` | `payment/services/paymentService` | Thanh toán, trang VNPay trả về |
| `GET /trips/operations`, `GET /trips/operations/events`, `POST /trips/{id}/status`, `POST /trips/{id}/sos` | `admin/services/operationsService` | Admin (chuyến, sự cố), Captain |
| `GET /tracking/boats` | `operationsService` | Điều hành trực tiếp |
| `GET /incidents`, `GET /incidents/{id}`, `POST /incidents/weather-check`, `POST /incidents/{id}/resolve`, `GET /incidents/weather-now` | `operationsService`, `weatherService` | Sự cố, tổng quan |
| `POST /refunds/request`, `POST /refunds/confirm-manual`, `POST /refunds/{id}/reconcile` | `admin/services/refundService` | Hoàn tiền |
| `PUT /checkins/windows/{tripStopCallId}` | `admin/services/checkInService` | Khung giờ check-in |
| `GET/POST/PUT /stations`, `GET/POST/PUT /boats` | `stationService`, `boatService` | Bến, tàu |
| `POST /texttospeech/generate`, `GET /texttospeech/audio`, `GET /audioguides/pois`, `POST /audioguides/publish` | `audioGuideService` | Thuyết minh |
| `POST /aiassistant/chat` | `assistant/services/assistantService` | Trợ lý AI |

Hub SignalR (`src/services/realtime.ts`, tự reconnect và join lại group): `/hubs/seat-sync` (ghế theo
đoạn, trạng thái chuyến; Guest chỉ đọc) và `/hubs/boat-tracking` (vị trí tàu, cảnh báo; cần đăng nhập).

## Guest và link quản lý vé

`POST /checkouts/holds` trả `accessToken` của đơn một lần duy nhất. FE gửi lại token này trong header
`X-Guest-Token`, lưu đơn đã thanh toán trên thiết bị (`tickets/orderStorage`) và tạo link
`/manage/{token}?orderId={id}` (BR-ID-02) để mở đơn ở máy khác.

## API backend chưa có (FE chưa làm phần tương ứng)

- Gửi email link quản lý vé sau thanh toán (BR-NOT-01).
- Lịch sử đơn theo tài khoản và liên kết đơn Guest vào tài khoản (BR-ID-04).
- Danh sách booking của chuyến bị huỷ để Admin hoàn tiền từng dòng (hiện nhập mã booking).
- `salesState` và giờ tại bến lên trong `GET /trips/search` (trang kết quả đang hiện giờ rời bến đầu).
- POI công khai cho trang Khám phá (`GET /audioguides/pois` chỉ cho Admin).
- Danh sách thiết bị, nhân viên và phân công scanner cho Admin (`POST /checkins/assignments` cần sẵn id).
- Tạo/sửa Route, Schedule, Trip và giá vé (BR-CFG-01).
