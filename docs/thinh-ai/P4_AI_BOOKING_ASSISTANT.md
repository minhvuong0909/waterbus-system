# AI Booking Assistant của Thịnh — backend cho P4-WEB-03

Roadmap repo có ID `P4-WEB-03` cho chat widget + Function Calling tạo **link đặt vé**, phụ thuộc Trip Search + Booking ổn định. Phần backend AI không có module ID riêng trong repo; ranh giới với frontend cần thống nhất với Viễn trước khi tích hợp.

Drive **đã giao Thịnh AI Booking Assistant** (Semantic Kernel, OpenAI/Gemini, plugin/function tìm chuyến và dữ liệu UI Card). Từ “optional” là trạng thái **widget trong roadmap repo**, không có nghĩa Thịnh được bỏ đầu việc này khỏi kế hoạch. Widget và render card ban đầu từ FE của Viễn trên `main`; phần nối card AI đến luồng xem chuyến hiện được thực hiện trong nhánh AI của Thịnh. Cần báo rõ file FE chạm vào khi tạo PR.

## Các bước phải hoàn thành

1. Dùng **Groq API qua Semantic Kernel**, model khởi đầu `openai/gpt-oss-20b` theo [AI_DECISIONS.md](AI_DECISIONS.md). Đây là lựa chọn mới của Thịnh, khác hai provider được Drive nêu (OpenAI/Gemini); cần nhóm xác nhận trước khi coi là đáp ứng phân công. Credential/config ngoài Git.
2. Định nghĩa input chat và response tách phần văn bản với `TripCard[]` có schema ổn định. Không dùng văn bản model để frontend tự bóc tách chuyến.
3. Định nghĩa plugin/function tìm chuyến: validate ngày, bến, loại dịch vụ và tham số được API hỗ trợ; gọi search API/query đã được owner xác nhận đúng. Không nêu bến thì lấy chuyến sắp tới; nêu một bến **không rõ chiều** thì tìm cả chuyến xuất phát và chuyến đến bến đó. Nếu khách nói rõ "điểm đến/bến đến Bạch Đằng" thì chỉ lọc bến đến; "bến đi/từ Bạch Đằng" thì chỉ lọc bến đi. Nêu hai bến thì lọc đúng chiều. Ngày là bộ lọc tùy chọn, kết quả rỗng thì báo rỗng. Function liệt kê bến gọi `GetStationsQuery` khi khách hỏi có những bến nào. Hiện API gốc chỉ lọc bến đầu/cuối tuyến, không hỗ trợ tìm mọi chuyến ghé qua bến trung gian.
4. Function tạo link đặt vé chỉ dùng service/route đã được owner Booking/Web cung cấp; không tự nối chuỗi URL từ phỏng đoán và không tạo Booking/Payment.
5. Thực thi giới hạn tool, timeout/rate limit, xử lý lỗi provider/API, redaction log và test prompt injection.
6. Kết nối với widget của Viễn, test live: hỏi giờ tàu → tool trả chuyến thật → Trip Card render đúng → link mở đúng luồng đặt vé.

Hiện `SearchTripsQuery` trong source có `BasePrice = 0` TODO và suy bến dừng theo `Station.OrderIndex`; không dùng giá/bến đó như sự thật cho người dùng trước khi owner sửa/xác nhận contract.

Backend có `MediatRTripSearchTool` (gọi cùng query mà API Trip Search dùng), `ConfiguredBookingLinkTool` (chỉ chấp nhận template HTTPS do owner cung cấp), DI và `POST /api/v1/AiAssistant/chat`. FE trên `main` đã nối widget với endpoint này; Thịnh bổ sung dữ liệu bến khách hỏi vào card và nút **Xem chuyến** để mở luồng `/trip` hiện có, nơi FE tải lại trip detail/seat map. Không tự tạo Booking/Payment. Link chính thức qua `ConfiguredBookingLinkTool` vẫn cần owner xác nhận contract.

Để demo nội bộ trước khi owner Trip Search sửa giá/bến, có cờ `EnableUnverifiedTripCardsForDevelopment` mặc định tắt, chỉ hoạt động khi host là Development. Card khi đó có thể hiển thị `0 ₫` như dữ liệu query hiện tại, kèm `isPriceVerified=false` và `isDevelopmentPreview=true` trong JSON. **Không coi 0 ₫ là giá bán thật** và không bật `IsTripSearchContractVerified` để lách gate. Ngoài Development, query chưa xác nhận hoặc giá 0 vẫn bị chặn. Mẫu link chính thức có `{tripId}` và tùy contract có `{boardingStationId}`, `{disembarkingStationId}`, `{passengerCount}`.

Lát cắt hội thoại cho demo nội bộ: `SearchTrips` hỗ trợ tìm chuyến sắp tới không bộ lọc, một bến (hai chiều), hai bến đúng chiều, ngày, loại chuyến, số khách, giá API (`priceGreaterThan`/`priceLessThan`, VND, so sánh nghiêm ngặt) và ghế trống tối thiểu (`minimumAvailableSeats`). Giờ "khoảng 16:00" dùng để xếp chuyến gần giờ nhất lên đầu; "sau 16:00"/"đến trước 17:00" là bộ lọc. Kết quả không còn bị cắt ở 3/10 chuyến trong adapter AI: câu hỏi "toàn bộ/tất cả chuyến" tìm mới, bỏ toàn bộ bộ lọc cũ; câu hỏi "toàn bộ chuyến còn ghế" chỉ áp dụng ghế trống tối thiểu 1. API trả `searchContext`; FE gửi lại để câu "sớm hơn", "đổi ngày", "thêm một người" không phải nhập lại mọi điều kiện. Câu trả lời liệt kê giờ, số ghế và giá từ thẻ do API trả, không dùng số do model tự tạo. Trong Development preview, giá `0 ₫` tham gia so sánh đúng giá trị API hiện có, nhưng **không phải giá bán đã xác nhận**. `ListStations` trả bến từ query thật; tìm theo khu vực chỉ gợi ý theo tên/địa chỉ bến đã có, không đoán GPS. Widget cho chọn card bằng nút hoặc mã A/B/C, xác nhận rằng chưa đặt vé, rồi chuyển `tripId` và tiêu chí sang trang chi tiết chuyến/chọn ghế. Số khách chuyển qua nhãn tìm kiếm; số ghế và giá cuối cùng vẫn phải kiểm tra ở luồng booking. Không triển khai trả lời chính sách vé/lên tàu vì chưa có nguồn nội dung chính thức.

Ngôn ngữ và ngữ cảnh: `POST /api/v1/aiassistant/chat` nhận thêm `languageCode: "en" | "vi"` (tùy chọn, mặc định `vi` cho client cũ). Widget gửi ngôn ngữ giao diện; câu trả lời, lỗi/đề nghị hỏi rõ, Trip Card và thao tác chọn chuyến dùng cùng ngôn ngữ, còn tên bến giữ nguyên dữ liệu API. `searchContext` được kế thừa khi khách bổ sung điều kiện như "dưới 50k", "còn ghế", "chiều mai", "sớm hơn" hoặc "đổi ngày"; backend buộc giữ bộ lọc cũ ngay cả khi model bỏ sót cờ `usePreviousSearch`. Câu hỏi "toàn bộ chuyến"/"all trips", "hiện tại có những chuyến nào"/"what trips are available", "tìm lại từ đầu"/"start over" là lần tìm mới: bỏ bộ lọc cũ trước khi xử lý điều kiện mới. Câu hỏi toàn bộ có nêu bến, ngày hoặc giá vẫn được AI phân tích như một lần tìm mới với các điều kiện **trong chính câu đó**, không bỏ điều kiện mới.

`searchContext.restrictSingleStationToRequestedDirection` (bool, mặc định `false` cho client cũ) giữ ý định khách đã chỉ rõ bến đi hoặc bến đến trong câu nối tiếp. Khi `false`, một bến không rõ chiều được tra cả hai chiều; khi `true`, adapter chỉ gọi Trip Search theo đúng trường `departureStationName` hoặc `arrivalStationName` đã chuẩn hóa.

Chỉ khi có `searchContext` cũ và câu mới hỏi chung chung, không nêu điều kiện bổ sung hay yêu cầu tìm mới rõ ràng (ví dụ "Có chuyến nào?"), API mới hỏi "toàn bộ hay lọc tiếp?" và trả `pendingClarification: { originalMessage }`, giữ nguyên `searchContext`. Câu "Dưới 50k" hoặc "Còn chuyến nào có ghế trống?" tự lọc tiếp, không hỏi phạm vi. Widget gửi lại cả hai field ở lượt sau khi thật sự có câu hỏi chờ. Khách trả lời "toàn bộ"/"all" thì xử lý câu gốc như lần tìm mới; "lọc tiếp"/"continue" thì xử lý câu gốc với bộ lọc cũ, kể cả khi model chọn sai `usePreviousSearch`. Câu trả lời chưa rõ tiếp tục giữ câu gốc; yêu cầu mới rõ ràng thì bỏ câu đang chờ. `pendingClarification` là `null` sau khi đã giải quyết. Không lưu phiên hội thoại ở server; reload trang hoặc bắt đầu cuộc hội thoại mới sẽ xóa trạng thái này trên widget.

Quy tắc thời gian cho trợ lý: AI tách thông tin bến/ngày/giờ/giá/ghế từ câu tự nhiên; backend ràng buộc thêm các mốc rõ ràng "hôm nay"/"today", "mai"/"tomorrow", "ngày kia" và buổi sáng/chiều/tối theo UTC+7. Vì vậy "chiều tối nay" chỉ tìm trong ngày hiện tại từ 16:00–22:00; không có thì báo không có chuyến trong ngày đó, không đưa chuyến ngày mai vào. "Sắp khởi hành"/"next departure" khác "toàn bộ chuyến": chỉ hiển thị tối đa 3 chuyến sớm nhất của ngày phục vụ gần nhất theo kết quả Trip Search. Đây là quy ước diễn giải AI của Thịnh cho demo, không sửa nghiệp vụ Trip Search/giờ chạy của teammate. Câu hỏi rộng như "Bạch Đằng có những chuyến nào?" bỏ bộ lọc cũ nhưng vẫn dùng bến mới trong chính câu hỏi.

### Demo local với dữ liệu Trip Search hiện tại

1. Chạy WebApi trong môi trường `Development`, có DB chứa Station/Trip/TripStopCall/ghế phù hợp. Giữ Groq API key trong User Secrets (không commit). Bật `AiBookingAssistant:Groq:IsEnabled=true` và `AiBookingAssistant:Integration:EnableUnverifiedTripCardsForDevelopment=true` qua User Secrets của `WaterbusSystem.WebApi`.
2. FE đặt `VITE_USE_MOCK=false`, chạy WebApi và Vite. Thử lần lượt "Hiện có chuyến nào sắp tới?", "Linh Đông có chuyến nào?", "Bạch Đằng đến Linh Đông có chuyến nào?"; `SearchTrips` tương ứng dùng không bến, một bến ở cả hai chiều, hoặc đúng cặp bến. Hỏi "Hiện có những bến nào?" để kiểm tra `ListStations`. Card từ API có thể hiển thị `0 ₫` trong Development preview.
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
