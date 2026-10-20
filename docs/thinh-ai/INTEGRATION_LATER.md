# Kế hoạch tích hợp phần AI của Thịnh với các module nhóm

File này tách các công việc cần hợp tác sau khi các thành viên hoàn thành module của họ. **Thịnh chỉ tích hợp TTS, geofence audio tại POI và AI Booking Assistant; không làm CRUD POI hoặc ETA.** Ghi rõ dependency, người/cụm module cần phối hợp, dữ liệu trao đổi và điều kiện nghiệm thu. Các payload/tên endpoint bên dưới là **contract cần chốt**, không phải API đã tồn tại.

## 1. Tình trạng source tại 15/10/2026 (sau merge `origin/main`)

| Thành phần | Hiện trạng trong repo | Tác động tới Thịnh |
|---|---|---|
| POI/RoutePoi/AudioGuide/BoatPositionEvent | Đã có entity, DbSet, EF mapping và migration | Có thể viết core TTS/geofence trên model hiện có. |
| BoatTrackingHub (`P3-BE-01`) | Đã có Hub, tracking REST/cache và GPS simulator; xem `docs/REALTIME_CONTRACT.md` | Đã nối GPS được chấp nhận vào bộ đánh giá geofence qua adapter DB, chỉ bật opt-in cho local demo. Chưa có Onboard Player/ACK thật; simulator mặc định tắt. |
| POI CRUD và quản lý kịch bản (`P3-BE-03`, `P3-WEB-02`) | Chưa thấy API/UI trong source; `AudioGuide` không có script text | Chưa thể quyết định cách lưu/duyệt kịch bản hay form Admin. |
| Audio storage/Onboard Player | Có Azure Blob MP3 adapter và service nhận script đã duyệt → TTS → Blob; smoke test MP3 thật đã pass ngày 15/10/2026. Dispatcher local hiện chỉ ghi log và **không** ACK; chưa có player/backend dispatch production | Cần account/container private cho môi trường nhóm, nguồn script/version của owner POI và contract player để truy cập URI, phát ra loa. Bài test dùng câu thử, không chứng minh kịch bản đã duyệt/AudioGuide/loa tàu chạy. |
| Trip Search | Có `SearchTripsQuery`/`GET /api/v1/trips/search`; giá đang là `0m` TODO, stop list suy từ Station.OrderIndex | AI chỉ được dùng sau khi owner Trip Search xác nhận dữ liệu giờ/giá/bến chính xác. |
| Booking link/Trip Card/chat widget | Backend có Semantic Kernel adapter, hai tool adapter, DI và `POST /api/v1/AiAssistant/chat`; widget FE đã gọi API thật và hiển thị card từ tool, không còn nhánh canned/mock cho Assistant | Cần owner Trip Search xác nhận giá/bến và owner Web/Booking xác nhận template link. Chỉ dùng cờ preview để demo Development; không coi giá `0` hay link chưa xác nhận là production-ready. |

Initializer hiện seed một Trip demo loại `Commuter` với stop calls. Có Trip demo **không chứng minh** luồng Sightseeing/audio chạy được; test tích hợp phải tạo Trip Sightseeing hợp lệ theo BR FINAL.

## 2. TTS → nội dung POI → file audio

**Đối tác:** người làm CRUD POI/`P3-WEB-02`, owner storage/hạ tầng và người duyệt nội dung Admin.

Cần chốt trước khi nối:

1. Script tiếng Việt được lưu trong entity/asset nào, version và trạng thái duyệt ra sao? `PointOfInterest`/`AudioGuide` hiện không có field script.
2. Voice/region/output format Azure Speech và credential dev/test do ai cung cấp?
3. MP3 lưu Azure Blob theo quyết định Thịnh: team cấp account/container/quyền nào, player đọc qua backend hay URL ngắn hạn? `AudioAssetUri` chỉ giữ URI/định danh bền vững, không giữ SAS hết hạn; giới hạn 500 ký tự có phù hợp?
4. Tạo lại audio xử lý file cũ, lỗi upload và unique `(PoiId, LanguageCode)` thế nào?
5. Ai nghe duyệt và chuyển `IsPublished = true`?

Contract cần thống nhất: `PoiId`, script/version, file MP3/content type/duration, URI bền vững, publish status. Test end-to-end: Admin tạo POI/kịch bản → sinh MP3 bằng Azure thật → lưu URI → nghe duyệt → chỉ audio `vi` đã publish được cung cấp cho playback. Không cần triển khai Passenger audio cá nhân.

## 3. BoatTrackingHub → geofence → Onboard Player

**Đối tác:** Kiệt/owner `P3-BE-01` và owner Onboard Player/Captain Web. ETA do phần khác của nhóm làm; Thịnh không tích hợp logic ETA trong task của mình.

Cần chốt trước khi nối:

1. Vị trí có `BoatId`, `TripId`, latitude, longitude, `ObservedAt`, source và tốc độ không? Xác thực Boat/Trip và chống mẫu GPS cũ ở đâu?
2. Route/Trip/RoutePoi nào được phục vụ, kể cả hành trình vòng và nhiều lần ghé cùng POI?
3. `TriggerRadiusM = null` đã chọn không trigger; xem `AUDIO_TRIGGER_DECISIONS.md`. Owner tuyến cần cấu hình bán kính dương.
4. Chống phát lặp đã chọn khóa `TripId + RoutePoiId + VisitId`; tracking/tiến độ cần cấp `NextRoutePoiDisplayOrder` và `VisitId` ổn định. Cần kho dedup production; không reset theo GPS.
5. BR FINAL đã chốt gửi đến Onboard Player/máy tính trên Boat. Contract do Thịnh đề xuất: `RequestId` ổn định, ACK player đã chấp nhận xếp lệnh trước khi đánh dấu trigger; ACK bắt đầu phát là trạng thái khác. Cần owner player xác nhận kênh/timeout/trạng thái và hiện thực trên thiết bị.
6. Dừng/nhường ưu tiên khi Suspended/Terminated/cảnh báo an toàn được thực thi ở backend, player hay cả hai?
7. Nếu nhóm có service ETA riêng, ranh giới không gian tên/DTO giữa nó và geofence POI là gì để tránh code trùng?

Contract tối thiểu cần thống nhất: vị trí Boat + thời gian + Trip; geofence result gồm `TripId`, `PoiId`, khoảng cách mét và audio `vi` đã publish; command/event tới Onboard Player; safety override và idempotency. `TriggerPOI` trong Drive chỉ là ví dụ, không dùng tên/payload đó nếu owner Hub chưa chốt.

Test end-to-end phần Thịnh: Boat trên Sightseeing Trip đi từ ngoài vào POI → một trigger hợp lệ → Onboard Player phát file đã duyệt; GPS lặp không phát trùng; Regular và Trip Suspended/Terminated không phát; thông báo an toàn ngắt audio. **ETA tới bến là tiêu chí của phần khác, không phải nghiệm thu Thịnh.**

## 4. AI Booking Assistant → Trip Search → Customer Web

**Đối tác:** Viễn (chat widget/Trip Card), owner Trip Search và Booking/link.

Cần chốt trước khi nối:

1. `P4-WEB-03` optional trong roadmap được làm ở phase/kỳ nào, Thịnh phụ trách backend tool-calling tới đâu và Viễn phụ trách UI tới đâu?
2. Thịnh đã đổi sang Groq `openai/gpt-oss-20b`; nhóm có chấp nhận khác lựa chọn OpenAI/Gemini trên Drive không, ai cấp key, giới hạn rate/quota và chi phí?
3. SearchTrips contract sau khi sửa các hạn chế hiện tại: giờ, bến thực sự phục vụ, loại chuyến, giá, số ghế có tin cậy không?
4. Trip Card JSON schema và link đặt vé hợp lệ được tạo bởi service nào? AI không tự dựng URL từ phỏng đoán.
5. Nếu khách hỏi hoàng hôn, có nguồn giờ hoàng hôn và vị trí đáng tin không? Nếu chưa có, assistant chỉ tư vấn theo lịch chuyến và nêu giới hạn dữ liệu.

Contract tối thiểu cần thống nhất: request tìm chuyến có bộ lọc hợp lệ; response Trip Card có `TripId`, giờ, route, loại chuyến, giá đã xác thực, availability và booking link do hệ thống tạo; error/empty result; timeout/rate limit. AI dùng API đã phân quyền theo `BR-AI-01`, không tự ý ghi Booking/Payment.

Test end-to-end: khách hỏi chuyến → tool gọi API thật → UI hiển thị đúng Trip Card → link mở đúng luồng đặt vé; dữ liệu không có thì assistant không bịa giá/giờ; prompt injection không vượt tool allowlist.

## 5. Quy tắc hoàn thành tích hợp

- Không đánh dấu module roadmap hoàn tất chỉ vì core hoặc fake provider pass test.
- Mỗi contract đã chốt phải được ghi lại trong PR/ADR hoặc tài liệu API của nhóm, rồi cập nhật file này.
- Chạy build/test và test end-to-end với module thật trước khi nghiệm thu DoD.
- Khi contract nhóm khác thay đổi, sửa adapter ở ranh giới; không sửa trực tiếp module của thành viên khác mà chưa phối hợp.

### Nghiệm thu “chạy thật”

Chỉ báo hoàn tất sau khi kiểm tra trong môi trường có các dịch vụ/thiết bị thật hoặc môi trường triển khai tương đương:

1. Azure Speech với credential cấu hình ngoài Git tạo MP3 tiếng Việt từ kịch bản đã duyệt; lỗi/quota/timeout hiển thị rõ, không tạo `AudioGuide` giả thành công.
2. MP3 được lưu bền vững và URL/cơ chế truy cập của Onboard Player hoạt động; file cũ/phiên bản mới không bị lẫn khi phát.
3. Một Trip Sightseeing có Route–POI–Boat hợp lệ nhận vị trí Boat thật hoặc vị trí mô phỏng theo `BR-TRK-01`; đúng một lần trigger ở POI, phát qua loa của thiết bị trên tàu; Regular không phát audio tham quan.
4. Khi Trip Suspended/Terminated hoặc có thông báo an toàn, audio nhường ưu tiên ngay; GPS gửi lặp, mất mạng và provider TTS lỗi có hành vi được ghi nhận rõ.
5. AI Assistant dùng provider/key thật và chỉ trích giờ/giá/bến từ API đã được owner xác nhận. Trip Card render đúng trên Web của Viễn; booking link mở đúng luồng. Nếu một trong các dữ liệu chưa đáng tin, assistant không trả như sự thật.
6. `dotnet build`, `dotnet test`, test API và demo xuyên Web → Backend → dịch vụ ngoài/Onboard Player đều có bằng chứng pass trong PR hoặc biên bản nghiệm thu.
