# API chi tiết chuyến và sơ đồ ghế

Hai endpoint công khai cho Passenger/Guest, không yêu cầu JWT hay X-Guest-Token. Không trả thông tin người mua, hành khách hoặc định danh Order.

## 1. Chi tiết chuyến

`GET /api/v1/trips/{tripId}`

Trả `tripId`, `routeId`, `routeName`, `tripType`, `status`, giờ khởi hành/đến UTC, `salesCloseAt`, `isDelayed`, `canBook`, `salesState`, `boat { boatId, code, name }` và `stops`.

Mỗi stop gồm `callId`, `stationId`, `stationName`, `visitOrder`, `canBoard`, `canAlight`, `status`, các giờ dự kiến và cửa sổ check-in. Đây là các TripStopCall thật của chuyến, không phải danh sách bến nội suy từ Station.OrderIndex. Dùng callId để phân biệt hai lần ghé cùng một bến trong chuyến vòng.

`canBook` của endpoint detail biểu diễn điều kiện mở bán của chuyến; cần gọi seat map để biết availability và giá cụ thể.

## 2. Sơ đồ ghế theo đoạn

`GET /api/v1/trips/{tripId}/seats?boardingCallId={callId}&disembarkingCallId={callId}`

Hai callId bắt buộc thuộc cùng Trip, đúng chiều VisitOrder, cho phép lên/xuống và không Skipped. Station vật lý có thể giống nhau nếu hai call khác nhau. Dùng cùng quy tắc chọn hành trình với checkout.

Ví dụ cấu trúc response (ID là GUID thật do database cung cấp):

```json
{
  "tripId": "11111111-1111-4111-8111-111111111111",
  "boatId": "22222222-2222-4222-8222-222222222222",
  "boardingCallId": "33333333-3333-4333-8333-333333333333",
  "disembarkingCallId": "44444444-4444-4444-8444-444444444444",
  "boardingVisitOrder": 1,
  "disembarkingVisitOrder": 3,
  "canBook": true,
  "salesState": "open",
  "availableSeatsCount": 1,
  "asOf": "2026-10-16T00:00:00+00:00",
  "seats": [{
    "seatId": "55555555-5555-4555-8555-555555555555",
    "seatCode": "S01",
    "rowNumber": 1,
    "columnNumber": 1,
    "seatClassId": "66666666-6666-4666-8666-666666666666",
    "seatClassName": "Standard",
    "price": 15000,
    "currency": "VND",
    "status": "available",
    "unavailableReason": null,
    "holdExpiresAt": null
  }]
}
```

Trạng thái ghế: `available`, `held`, `sold`, `unavailable`. Trạng thái được tính riêng cho đoạn `[boardingVisitOrder, disembarkingVisitOrder)`: A→B và B→C không chồng lấn. `sold` bao gồm reservation đã xác nhận/hoàn tất/chấm dứt nhưng chưa được nhả theo domain; reservation Cancelled không chiếm ghế. Ghế inactive vẫn có trong grid nhưng không chọn được; ghế của tàu khác và bản ghi soft-delete không được xuất.

Giá lấy từ FareRule đang hiệu lực theo TripType × SeatClass, không nhân khoảng cách/số bến. Thiếu giá → `unavailable` với `missingFare`; nhiều giá trùng hiệu lực → 409 để Admin xử lý. Tiền chưa phải cam kết: checkout vẫn tính và xác nhận giá tại lúc đặt.

`salesState`: `open`, `salesClosed`, `tripUnavailable`, `boatUnavailable`, `journeyUnavailable`. Khi chuyến đóng bán/đã khởi hành/không khả dụng, API vẫn cho xem grid nhưng các ghế là unavailable và canBook=false. Nếu chưa gán Boat thì seat map trả 409; detail vẫn trả 200 để hiển thị tình trạng chuyến.

## 3. Đồng bộ với checkout và SignalR

1. Lấy trip từ search, gọi detail để có callId thật.
2. Gọi seat map với hai callId; render grid bằng rowNumber/columnNumber, chọn seatId GUID.
3. Gửi tripId, seatIds, boardingStationId/disembarkingStationId và boardingCallId/disembarkingCallId vào `POST /api/v1/checkouts/holds`.
4. Tiếp tục submit, tạo URL thanh toán và lấy vé/QR theo `NHAT_ANH_COMMERCIAL_API.md`.
5. Event SignalR chỉ tác động nếu đoạn event chồng lấn đoạn đang xem. Sau reconnect và khi hold hết hạn, refetch seat map. Không gán một trạng thái cho ghế trên toàn Trip.

API GET không tạo/nhả hold, không ghi database, không phát Ticket. Pending reservation vẫn chiếm ghế cho tới khi cleanup chuyển thành Cancelled; `holdExpiresAt` là deadline DB, có thể đã qua trong khoảng đợi cleanup. Cách đọc này khớp lớp kiểm tra availability của checkout, tránh báo ghế trống nhưng POST vẫn bị từ chối vì reservation chưa được nhả. Không dựa vào cache Redis để tự bỏ qua reservation còn hiệu lực trong SQL.

Availability chỉ là snapshot. Khách khác có thể đặt sau GET; POST hold vẫn là nơi quyết định cuối cùng và có thể trả 409. Không cache response seat map ở proxy/client.

## 4. Lỗi và kiểm thử

- 400 ProblemDetails: GUID rỗng, thiếu/sai cặp call, ngược chiều hoặc điểm dừng không cho lên/xuống.
- 404: Trip không có hoặc đã soft-delete.
- 409: chưa có tàu hoặc bảng giá xung đột.

Test tự động: `dotnet test waterbus-be/WaterbusSystem.sln --filter FullyQualifiedName~TripReadQueriesTests`.

Kiểm chứng ngày 2026-10-16: 14 test mới pass; toàn bộ suite chạy với SQL Server/Redis Docker đạt 464 pass, 0 fail, 0 skip. Smoke test qua HTTP trên database seed riêng đã xác nhận: search → detail → lấy ghế thật → POST hold → ghế held → DELETE checkout → ghế available; đồng thời kiểm tra 400 khi thiếu callId, 404 kèm traceId, Cache-Control no-store và route trong Swagger. Đây chưa phải test thanh toán VNPay đầu cuối.

Việc này bổ sung API backend. Web vẫn cần thay getTripDetail/getSeatMap mock bằng hai endpoint này. SearchTrips hiện còn phần legacy cần chỉnh riêng (giá placeholder và nội suy stop từ Station.OrderIndex); không dùng dữ liệu stop/giá của search làm nguồn chuẩn cho chọn ghế.
