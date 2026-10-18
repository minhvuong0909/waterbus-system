# Bối cảnh dự án và rule bắt buộc cho phần AI/Smart Audio

## 1. Kiến trúc hiện tại

Backend dùng .NET 8 và Clean Architecture:

- `WaterbusSystem.Domain`: entity, enum, domain exception.
- `WaterbusSystem.Application`: CQRS feature slice, MediatR, validator, interface.
- `WaterbusSystem.Infrastructure`: EF Core, provider bên ngoài, persistence, DI.
- `WaterbusSystem.WebApi`: controller và middleware HTTP.
- `WaterbusSystem.UnitTests`: unit tests bằng xUnit/FluentAssertions.

Controller phải mỏng, chỉ nhận HTTP input, gọi `Mediator.Send(...)` và trả response. Business logic không đặt trong controller.

## 2. Phân công của Thịnh và ranh giới module

Tab `module` trên Google Drive **giao rõ Thịnh làm AI Developer** với hai đầu việc:

1. **AI Booking Assistant:** Semantic Kernel kết nối OpenAI/Gemini; plugin/function hiểu yêu cầu của khách, gọi API tìm chuyến và trả dữ liệu cho UI Card.
2. **AI Sightseeing Audio:** Azure AI Text-to-Speech sinh MP3 tiếng Việt từ kịch bản; logic đo khoảng cách geofence để kích hoạt audio khi tàu đến POI.

Đây là phân công của Thịnh. Roadmap repo **không gán tên người** và mô tả module rộng hơn. Phần tracking/realtime có owner khác theo Drive; khi tích hợp phải thống nhất contract, không tự sửa module của thành viên đó.

### Module Phase 3 có trong roadmap

- `P3-BE-03`: CRUD POI + Azure TTS. Roadmap ghi không có dependency và DoD là tạo một POI rồi có file audio khả dụng.
- `P3-BE-02`: Geofence + ETA, **phụ thuộc `P3-BE-01` BoatTrackingHub**. Roadmap nêu NetTopologySuite và tính khoảng cách tới cả POI lẫn bến.
- `P3-WEB-02`: UI quản lý POI/kịch bản là module Web riêng, phụ thuộc `P3-BE-03`.

**Phạm vi đã được Thịnh chốt:** chỉ nhận TTS và geofence POI trong hai module này. **CRUD POI và ETA không thuộc task của Thịnh**, dù roadmap gộp chúng trong cùng dòng module. Agent không được tự triển khai, test thay hoặc sửa contract của hai phần đó; chỉ đọc kết quả do nhóm cung cấp để tích hợp.

### Optional theo roadmap

- AI Booking Assistant **đã được Drive giao cho Thịnh**. `P4-WEB-03` là widget **optional trong roadmap repo**, phụ thuộc Trip Search + Booking ổn định. Đây là khác biệt về ưu tiên/thời điểm giữa hai tài liệu, không phủ nhận phân công AI. Repo gốc chưa chốt provider/model; Thịnh đã đổi sang Groq `openai/gpt-oss-20b` trong [AI_DECISIONS.md](AI_DECISIONS.md), cần báo nhóm vì Drive nêu OpenAI/Gemini.

### Không thuộc phạm vi này

- `BoatTrackingHub` là `P3-BE-01`, một module độc lập.
- UI Web quản trị POI là `P3-WEB-02`, cần giao riêng.
- Mobile Staff Scanner.
- Weather worker, DevOps, Payment, QR và Booking engine.

## 3. Business Rules áp dụng trực tiếp

Các rule dưới đây trích ý nghĩa từ BR FINAL; khi cần chi tiết phải đọc tài liệu gốc.

- `BR-SCOPE-01`: hệ thống có chế độ Sightseeing.
- `BR-SCOPE-02`: Passenger và Captain dùng Web; mobile chỉ dành cho Staff Scanner. Không thiết kế Passenger/Captain mobile riêng.
- `BR-ROUTE-02..06`: RouteStop có thứ tự và hướng phục vụ; Regular và Sightseeing có Route dịch vụ riêng (`BR-ROUTE-03`), Sightseeing có hành trình được cấu hình. Đọc nguyên văn trước khi validate Route/POI.
- `BR-TRK-01`: tracking dùng vị trí Boat, không dùng GPS Passenger; vị trí mô phỏng được chấp nhận cho demo.
- `BR-ID-06`: TripAccess không mở khóa audio cá nhân.
- `BR-EXP-01`: Sightseeing có Route riêng, POI theo hành trình và tracking theo vị trí Boat; Regular không mặc nhiên dùng chương trình Sightseeing.
- `BR-EXP-02`: audio Sightseeing phát chung qua loa của Boat, không phát riêng theo thiết bị Passenger; chỉ tiếng Việt trong scope hiện tại.
- `BR-EXP-03`: `AudioGuide` là nội dung đã chuẩn bị/kiểm duyệt; Onboard Player mới là thiết bị trực tiếp phát ra loa.
- `BR-EXP-04`: playback phụ thuộc Trip/Boat/POI, không phụ thuộc từng Ticket đã check-in.
- `BR-EXP-05`: geofence phải ngăn phát trùng không mong muốn trong cùng lượt hành trình.
- `BR-EXP-06`: audio phải dừng/nhường ưu tiên khi Trip bị Suspended/Terminated hoặc có thông báo an toàn.
- `BR-EXP-07`: playback offline là cải tiến tùy chọn, không phải yêu cầu bắt buộc của phiên bản môn học.
- `BR-CFG-01`: Admin quản lý nội dung POI/audio.
- `BR-AI-01`: nếu có AI Assistant, AI hướng dẫn/truy vấn qua API đã phân quyền; không tự ý vượt rule nghiệp vụ hoặc ghi thanh toán/check-in/vận hành đặc quyền.
- Mục 15 Logical direction: `Route` liên kết `PointOfInterest` qua `RoutePOI`; `PointOfInterest 1:N AudioGuide`.

Nếu tên/số BR thay đổi trong tương lai, nội dung BR FINAL mới nhất thắng tài liệu này.

## 4. Domain model hiện có phải tái sử dụng

### `PointOfInterest`

- `Name`, `Description`.
- `Latitude`, `Longitude`.
- `IsActive`.

### `RoutePoi`

- `RouteId`, `PoiId`.
- `DisplayOrder`: thứ tự POI trên Route.
- `TriggerRadiusM`: bán kính kích hoạt tính bằng mét.
- `IsActive`.
- Unique index `(RouteId, DisplayOrder)`.

### `AudioGuide`

- `PoiId`.
- `LanguageCode`, mặc định `vi`.
- `AudioAssetUri`.
- `DurationSeconds`.
- `IsPublished`.
- Unique index `(PoiId, LanguageCode)`.

Không tạo thêm bảng `PassengerAudioAccess`, `AudioUnlock`, `GeofenceEvent` hoặc bảng tương tự nếu chưa được duyệt.

## 5. Rule kỹ thuật

- Read query dùng `AsNoTracking()`.
- Create/update input dùng FluentValidation.
- Thao tác cấu hình POI/audio do Admin quản lý theo `BR-CFG-01`; policy `AdminOnly` đã có trong source. Contract truy cập đọc POI/audio cần thống nhất với Web/Onboard Player.
- Không hard delete record đã được tham chiếu; ưu tiên `IsActive = false` theo pattern hiện có.
- Phải xử lý `RowVersion`/optimistic concurrency khi update entity có `RowVersion`.
- Tọa độ hợp lệ: latitude `[-90, 90]`, longitude `[-180, 180]`.
- `TriggerRadiusM` hiện là **nullable** trong entity và Logical ERD. Nếu được cấu hình thì phải dương; cách xử lý khi null phải thống nhất với nhóm trước khi tích hợp playback.
- Tính khoảng cách bằng công thức/phép chiếu địa lý phù hợp; không dùng khoảng cách Euclidean trực tiếp trên degree.
- Không gọi Azure TTS trong Domain/Application handler bằng SDK cụ thể; Application phụ thuộc interface, Infrastructure implement provider.
- External call phải nhận `CancellationToken`, có timeout và log lỗi không chứa secret.
- Không trả Azure key/token trong DTO hoặc log.
- `AudioAssetUri` phải là URI/đường dẫn bền vững do storage abstraction trả về, không phải đường dẫn temp trên máy dev.
- TTS retry không được tạo nhiều `AudioGuide` trùng `(PoiId, vi)`; schema hiện có unique index trên cặp này.

## 6. Những mâu thuẫn đã được giải quyết

- Drive/roadmap cũ nói Passenger hoặc Captain mobile: **không áp dụng**, BR FINAL chỉ có Staff mobile.
- Drive từng nói audio cá nhân/đa ngôn ngữ: **không áp dụng**, hiện chỉ `vi`, phát chung qua loa Boat.
- Roadmap cũ có Voucher 110%: **không áp dụng**, BR FINAL loại Voucher/Wallet.
- Tài liệu cũ có Dynamic TOTP QR: phần AI/audio không được dựa vào đó; BR FINAL hiện chốt QR tĩnh ký số theo Booking.
- Roadmap ghi Dispatcher: BR FINAL không có Dispatcher; Admin đảm nhiệm điều phối.

## 7. Câu hỏi cần Team Lead xác nhận

Các câu hỏi này không chặn geofence core và TTS adapter độc lập, nhưng chặn tích hợp chạy thật:

1. Azure TTS resource region, voice tiếng Việt và output format nào được chọn?
2. Thịnh đã chọn Azure Blob cho MP3. Team có đồng ý dùng account/container Azure Blob và ai cấp credential/quyền truy cập?
3. Nội dung/kịch bản TTS lưu ở đâu? Entity hiện chưa có `ScriptText`, trong khi `P3-WEB-02` yêu cầu UI quản lý kịch bản. Quyết định này ảnh hưởng schema và API, cần chốt trước TTS CRUD.
4. BR FINAL đã chốt Onboard Player phát ra loa; backend gửi trigger bằng giao thức/event nào, player ACK ra sao và ai là owner implementation?
5. AI Booking Assistant đã giao Thịnh trên Drive và Thịnh chọn Groq (khác OpenAI/Gemini của Drive); nhóm có chấp nhận không, roadmap để widget optional ở phase/kỳ nào, ai sở hữu frontend widget và ai cấp API key/quota?
6. Khi các phần CRUD POI, ETA, `BoatTrackingHub` và UI Web hoàn thành, owner cung cấp contract nào để Thịnh tích hợp? Thịnh không nhận implement CRUD POI hoặc ETA.

Không tự đoán câu trả lời bằng cách sửa schema hoặc khóa chặt provider.
