# Audit FluentValidation và Business Invariants

Baseline: `main` sau PR #84 (`ae26d7b`). Bản này thay cho bản audit đầu, đã đối chiếu từng finding với code và BR v2.0.
Mỗi finding ghi rõ **tầng** (Validator, Service, Domain, BR) để không sửa trùng hoặc đổi nghiệp vụ.

## 1. Kết quả theo finding

| # | Finding | Tầng | Kết luận | Xử lý |
|---|---|---|---|---|
| 1 | `CreateStation/UpdateStationRequestValidator` không kiểm tra Latitude và Longitude | Validator | **Đúng, đã sửa** | Latitude `[-90, 90]`, Longitude `[-180, 180]` (nhận cả hai biên, từ chối NaN và vô cực). Không hardcode địa bàn TP.HCM hoặc Đồng Nai vì BR chưa chốt. |
| 2 | Chuyến thường (Regular/Commuter) cho chọn cùng một Station làm bến lên và bến xuống khi tuyến lặp bến | **Service/Domain** | **Đúng, đã sửa** | Quy tắc dùng chung `JourneyRules` (Domain), áp trong `BookingCreationService.ResolveJourneyCalls` (đặt vé trực tiếp, giữ chỗ checkout, sơ đồ ghế) và `TripService.SearchAsync`. Sightseeing dạng vòng vẫn được (BR-ROUTE-06). Không thêm `BoardingStationId != DisembarkingStationId` cho mọi chuyến. |
| 3 | Thiếu `NotEmpty()` cho `CheckoutPassengerRequest.BookingId/SeatId`, `CreateVnPayUrlRequest.OrderId`, `GetBoatPositionHistoryRequest.BoatId`; phần tử `null` trong `Passengers` | Validator | **Đúng một phần, đã sửa** | Thêm các rule trên và `NotNull()` cho từng phần tử `Passengers` (checkout và đặt vé). Lưu ý: `CheckoutService.SubmitAsync` đã đối chiếu hành khách với ghế đang giữ, nên GUID sai **không** lọt xuống database; đây là kiểm tra sớm với thông báo rõ. |
| 4 | "`Passengers` bắt buộc phải nhập đủ" | Validator + Service | **Diễn đạt sai trong audit cũ, không đổi code** | Đúng là: *nếu có cung cấp danh sách hành khách thì số lượng phải khớp số ghế*. Danh sách null hoặc rỗng vẫn hợp lệ, service dùng thông tin người mua làm hành khách. Giữ nguyên. Có test khoá hành vi này. |
| 5 | `OccurredAtDevice` cần ngưỡng 24 giờ ở validator | Service | **Không áp dụng, không đổi code** | `CheckInProcessor` đã xử lý: quét **online** dùng giờ server; quét **offline** bị `InvalidDeviceTime` nếu giờ thiết bị ở tương lai hoặc lùi quá `MaxOfflineSkewHours` (mặc định **6 giờ**, cấu hình được). Không thêm ngưỡng 24 giờ vì sẽ lệch với service. Không thêm validation ở request để không phá việc xử lý từng event độc lập trong một lô offline. Bổ sung test biên 6 giờ. |
| 6a | `PaymentsController.create-vnpay-url` chưa có validator | Validator + Controller | **Đúng, đã sửa** | Thêm `CreateVnPayUrlRequestValidator` và gắn `[ServiceFilter(typeof(ValidateRequestFilter))]` **chỉ** cho action này. IPN, return và reconcile không gắn filter (xác thực bằng chữ ký HMAC, dữ liệu nằm ở query/route). Có test khoá việc đó. |
| 6b | `TrackingController.GetHistory` cần filter | Service | **Không cần filter** | Controller nhận tham số riêng rồi tạo DTO; `BoatTrackingService.GetPositionHistoryAsync` đã gọi validator trực tiếp. Chỉ thêm rule `BoatId.NotEmpty()`. Không đổi cách nhận DTO nên không đổi API contract. |

## 2. Đính chính phần trích dẫn BR

### Quy tắc từ BR v2.0 và giới hạn do team chọn khi triển khai
| Giới hạn | Nguồn | Ghi chú |
|---|---|---|
| Tối đa 10 ghế mỗi lượt đặt | **Giới hạn triển khai** (BR không chốt con số). Có ở `CreateBooking`, `CreateCheckoutHold`, `CreateCheckout`, `UpdateCheckoutSeats` validators | Đổi cần cập nhật đồng loạt 4 validator |
| Tối đa 31 ngày mỗi lần sinh lịch | **Giới hạn triển khai** (`TripPlanningValidators`); truy vấn vận hành cũng giới hạn 31 ngày (`GetTripOperationsRequestValidator.MaxRangeDays`) | BR không chốt |
| Tối đa 50 điểm dừng mỗi Route | **Giới hạn triển khai** (`RouteValidators`) | BR không chốt |
| Cửa sổ tìm kiếm 14 ngày, tối đa 1000 ứng viên | **Giới hạn triển khai** (`TripService`) | Chặn truy vấn không giới hạn |
| Offline check-in lùi tối đa 6 giờ | **Cấu hình** (`CheckIn:OfflineReconciliation:MaxOfflineSkewHours`); BR-OFF-04/05 không quy định số cụ thể | Chỉnh theo vận hành |

### Tham chiếu BR cho các quy tắc nghiệp vụ
- **Incident không tự đổi `Trip.Status`:** tham chiếu **BR-INC-03** (phát hiện Incident không tự động Cancel, Suspend, Terminate Trip, Admin quyết định) và **BR-INC-04** (ghi nhận Incident và quyết định đổi TripStatus là hai nghiệp vụ khác nhau), cùng **BR-OPS-04** (Admin ghi nhận các chuyển trạng thái vận hành). BR-INC-02 chỉ nói về phạm vi Incident (Trip, Boat, Route), không phải căn cứ chính.
- **Lên và xuống cùng một Station:** **BR-ROUTE-06** (Sightseeing dạng vòng được phép, hai lần ghé khác VisitOrder; tránh áp máy móc `FromStationId != ToStationId` của Regular).

### Audio
`StoreAudioRequestValidator` đã kiểm tra kích thước (tối đa 50 MB), chữ ký MP3 (header ID3 hoặc MPEG), định dạng `audio/mpeg` và `AssetKey` (mẫu chữ-số-gạch, đuôi `.mp3`, chống path traversal). Không nên kết luận "an toàn tuyệt đối" chỉ từ kiểm tra header: header không chứng minh toàn bộ nội dung là MP3 hợp lệ hay vô hại.

## 3. Hồi quy đã thêm
- **Tọa độ bến:** biên `-90`, `90`, `-180`, `180` được nhận; ngoài biên, NaN, vô cực bị từ chối (cả Create và Update).
- **GUID.Empty và phần tử null:** `SubmitCheckoutRequestValidator` (BookingId, SeatId, phần tử null), `CreateVnPayUrlRequestValidator`, `GetBoatPositionHistoryRequestValidator`.
- **Hành trình:** chuyến thường cùng bến bị từ chối ở đặt vé, giữ chỗ, sơ đồ ghế và tìm kiếm; Sightseeing dạng vòng vẫn được; hai bến khác nhau trên chuyến thường vẫn được; tìm kiếm chuyến thường vòng không chọn bến đến thì lấy điểm xuống cuối ở bến khác.
- **Checkout không truyền danh sách `Passengers`:** validator chấp nhận null và rỗng; service dùng tên người mua cho từng ghế.
- **Thanh toán:** `create-vnpay-url` có validator; IPN, return và reconcile không đi qua `ValidateRequestFilter`.
- **Tracking:** `BoatId` rỗng bị từ chối, các rule cũ (khoảng 7 ngày, phân trang) giữ nguyên.
- **Check-in:** online dùng giờ server dù giờ thiết bị sai; offline đúng 6 giờ vẫn nhận, vượt 6 giờ thì `InvalidDeviceTime`; một event sai giờ không làm hỏng các event khác trong lô.

## 4. Thay đổi hành vi cần biết
- Test cũ `CircularTrip_ShouldMatchRepeatedStationAndKeepVisitOrderInStops` khẳng định một chuyến **Commuter** vòng tìm được A đến A. Theo BR-ROUTE-06 đó là hành vi chỉ đúng cho Sightseeing, nên test được sửa sang Sightseeing và thêm test cho Commuter.
- `create-vnpay-url` với `orderId` rỗng nay trả **400** (trước đó bị chặn ở bước kiểm tra quyền truy cập đơn và trả 403). Hợp đồng Swagger đã khai báo 400. Các `orderId` khác Guid rỗng giữ nguyên hành vi.
- Phối hợp với PR #81 (Kiệt, chặn bến trùng tọa độ): #81 sửa `StationService` (kiểm tra trùng toạ độ trong service, lỗi gắn vào `Latitude`, `Longitude`), không đụng validator. Hai thay đổi bổ sung cho nhau: validator chặn toạ độ không tồn tại, service chặn trùng toạ độ.
