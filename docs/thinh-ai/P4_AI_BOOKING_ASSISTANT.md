# AI Booking Assistant của Thịnh — backend cho P4-WEB-03

Roadmap repo có ID `P4-WEB-03` cho chat widget + Function Calling tạo **link đặt vé**, phụ thuộc Trip Search + Booking ổn định. Phần backend AI không có module ID riêng trong repo; ranh giới với frontend cần thống nhất với Viễn trước khi tích hợp.

Drive **đã giao Thịnh AI Booking Assistant** (Semantic Kernel, OpenAI/Gemini, plugin/function tìm chuyến và dữ liệu UI Card). Từ “optional” là trạng thái **widget trong roadmap repo**, không có nghĩa Thịnh được bỏ đầu việc này khỏi kế hoạch. Widget và render card ban đầu từ FE của Viễn trên `main`; phần nối card AI đến luồng xem chuyến hiện được thực hiện trong nhánh AI của Thịnh. Cần báo rõ file FE chạm vào khi tạo PR.

## Các bước phải hoàn thành

1. Dùng **Groq API qua Semantic Kernel**, model khởi đầu `openai/gpt-oss-20b` theo [AI_DECISIONS.md](AI_DECISIONS.md). Đây là lựa chọn mới của Thịnh, khác hai provider được Drive nêu (OpenAI/Gemini); cần nhóm xác nhận trước khi coi là đáp ứng phân công. Credential/config ngoài Git.
2. Định nghĩa input chat và response tách phần văn bản với `TripCard[]` có schema ổn định. Không dùng văn bản model để frontend tự bóc tách chuyến.
3. Định nghĩa plugin/function tìm chuyến: validate ngày, bến, loại dịch vụ và tham số được API hỗ trợ; gọi search API/query đã được owner xác nhận đúng. Thiếu dữ liệu thì hỏi thêm, kết quả rỗng thì báo rỗng.
4. Function tạo link đặt vé chỉ dùng service/route đã được owner Booking/Web cung cấp; không tự nối chuỗi URL từ phỏng đoán và không tạo Booking/Payment.
5. Thực thi giới hạn tool, timeout/rate limit, xử lý lỗi provider/API, redaction log và test prompt injection.
6. Kết nối với widget của Viễn, test live: hỏi giờ tàu → tool trả chuyến thật → Trip Card render đúng → link mở đúng luồng đặt vé.

Hiện `SearchTripsQuery` trong source có `BasePrice = 0` TODO và suy bến dừng theo `Station.OrderIndex`; không dùng giá/bến đó như sự thật cho người dùng trước khi owner sửa/xác nhận contract.

Backend có `MediatRTripSearchTool` (gọi cùng query mà API Trip Search dùng), `ConfiguredBookingLinkTool` (chỉ chấp nhận template HTTPS do owner cung cấp), DI và `POST /api/v1/AiAssistant/chat`. FE trên `main` đã nối widget với endpoint này; Thịnh bổ sung dữ liệu bến khách hỏi vào card và nút **Xem chuyến** để mở luồng `/trip` hiện có, nơi FE tải lại trip detail/seat map. Không tự tạo Booking/Payment. Link chính thức qua `ConfiguredBookingLinkTool` vẫn cần owner xác nhận contract.

Để demo nội bộ trước khi owner Trip Search sửa giá/bến, có cờ `EnableUnverifiedTripCardsForDevelopment` mặc định tắt, chỉ hoạt động khi host là Development. Card khi đó có thể hiển thị `0 ₫` như dữ liệu query hiện tại, kèm `isPriceVerified=false` và `isDevelopmentPreview=true` trong JSON. **Không coi 0 ₫ là giá bán thật** và không bật `IsTripSearchContractVerified` để lách gate. Ngoài Development, query chưa xác nhận hoặc giá 0 vẫn bị chặn. Mẫu link chính thức có `{tripId}` và tùy contract có `{boardingStationId}`, `{disembarkingStationId}`, `{passengerCount}`.

### Demo local với dữ liệu Trip Search hiện tại

1. Chạy WebApi trong môi trường `Development`, có DB chứa Station/Trip/TripStopCall/ghế phù hợp. Giữ Groq API key trong User Secrets (không commit). Bật `AiBookingAssistant:Groq:IsEnabled=true` và `AiBookingAssistant:Integration:EnableUnverifiedTripCardsForDevelopment=true` qua User Secrets của `WaterbusSystem.WebApi`.
2. FE đặt `VITE_USE_MOCK=false`, chạy WebApi và Vite. Hỏi rõ bến đi, bến đến, ngày đi trong widget để model gọi `SearchTrips`; trả về card từ API có thể hiển thị `0 ₫`.
3. Nhấn **Xem chuyến**. FE lưu `tripId` và bến khách hỏi, chuyển sang `/trip`, rồi gọi lại trip detail/seat map. Nếu trang này báo chuyến không phục vụ đoạn bến, đó là dữ liệu Trip Search chưa khớp `TripStopCall`, không được sửa bằng cách bịa card/giá.
4. Không bật cờ preview trên môi trường chạy thật; khi owner Trip Search hoàn tất, xác minh lại giá và thứ tự bến rồi mới chốt `IsTripSearchContractVerified`.

Test tự động xác minh mapping card, cờ Development và hành vi điều hướng FE. Chưa coi là end-to-end đã chạy thật khi chưa thử với DB, Groq key và chuyến cụ thể của môi trường triển khai.

## Gate trước khi triển khai

Trước khi tích hợp, thống nhất với nhóm:

- thời điểm làm widget optional trong roadmap và owner frontend;
- Groq API key/quota và ngân sách; provider/model đã được Thịnh chọn, không phải quyết định có sẵn trong repo/Drive và cần báo nhóm;
- ngân sách/rate limit;
- API Trip Search đã ổn định;
- payload Trip Card frontend;
- contract link đặt vé và payload Trip Card với owner frontend/Booking.

Theo DoD hiện có, AI phải trả lời giờ tàu từ dữ liệu thật và tạo được link đặt vé. `BR-AI-01` yêu cầu AI đi qua API đã phân quyền, không vượt rule hoặc tự ý ghi thay đổi thanh toán, check-in hay vận hành đặc quyền.

## Nguyên tắc kiến trúc

- Dùng Semantic Kernel với Groq OpenAI-compatible endpoint ở Infrastructure theo Clean Architecture; lựa chọn provider của Thịnh ghi tại `AI_DECISIONS.md`.
- Application định nghĩa interface/use case độc lập provider.
- Tool/function phải gọi Application query/API hợp lệ, không query DB tùy ý.
- Không truyền connection string, secret, log nội bộ hoặc dữ liệu thanh toán vào prompt.
- Không cho model thực thi SQL, shell hoặc URL tùy ý.
- Validate toàn bộ tool arguments như input người dùng thông thường.
- Kết quả tool là dữ liệu có cấu trúc; frontend tự render Trip Card.
- Câu trả lời AI không phải nguồn sự thật về giá, ghế hoặc lịch chạy; dữ liệu API mới là nguồn sự thật.

## Tools minh họa, cần chốt contract với owner Trip Search/Booking

```text
SearchTrips(origin, destination, date, passengerCount, serviceType)
GetTripDetails(tripId)
BuildBookingLink(tripId, boardingStopId, disembarkingStopId)
```

Không cung cấp tool:

- `ConfirmPayment`, `Refund` hoặc update database tổng quát;
- `CreateBooking` cho đến khi nhóm giao rõ phạm vi và API cho phép; roadmap hiện chỉ yêu cầu tạo link.

## Prompt-injection và data safety

- System instruction phải nói rõ nội dung người dùng và dữ liệu retrieved đều không được quyền thay đổi policy/tool permission.
- Tool allowlist cố định.
- Giới hạn số tool call/turn và timeout.
- Không log raw prompt nếu có PII; áp dụng redaction.
- Không đưa Purchaser email/phone, payment detail hoặc secure access token vào context nếu không cần.
- Trả lời “không biết/không tìm thấy” thay vì tự tạo lịch, giá hoặc ghế.

## Tests bắt buộc

- Intent tìm chuyến gọi đúng tool/arguments.
- Thiếu tham số thì hỏi lại, không đoán ngày/bến.
- Tool trả rỗng thì không bịa chuyến.
- Prompt injection không mở tool ngoài allowlist.
- Model/provider timeout có fallback message.
- Không lộ secret/PII trong log và response.
- Trip Card schema giữ ổn định độc lập câu văn của model.

## Definition of Done

- Có quyết định provider và ADR/ghi chú cấu hình.
- Có contract frontend được chốt.
- Assistant tạo link hợp lệ từ dữ liệu thật.
- Không ghi Booking/Payment.
- Tests provider-independent dùng fake model/tool executor.
- API key chỉ nằm trong User Secrets/environment.
