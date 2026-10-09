# Việc Thịnh có thể giao Antigravity làm ngay

Ngày kiểm tra source: 09/10/2026. Mục tiêu của file này là tạo **phần code độc lập, kiểm thử được**, sau đó nối với các module nhóm qua [INTEGRATION_LATER.md](INTEGRATION_LATER.md). Các phần dưới đây **chưa phải hoàn tất toàn bộ DoD** của `P3-BE-02` hoặc `P3-BE-03`.

## Nguyên tắc giao việc

- Giao **một lát cắt mỗi lần** trong branch/PR riêng; không giao toàn bộ phần AI bằng một prompt.
- Đọc `README.md`, `DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md`, `PROJECT_CONTEXT_AND_RULES.md`, BR FINAL liên quan, source hiện tại và `DELIVERY_WORKFLOW.md`.
- Code từ interface/dữ liệu hiện có; không thêm migration, endpoint, event SignalR hoặc sửa module của Kiệt/Viễn trong hai lát cắt đầu.
- Nếu agent phát hiện một quyết định cần đổi schema/API chung, ghi vào `INTEGRATION_LATER.md` và tiếp tục phần thuần không phụ thuộc quyết định đó.

## Lát cắt 1 — Geofence core cho POI

**Giao được ngay.** Dựa trên `PointOfInterest.Latitude/Longitude` (`decimal?`), `RoutePoi.TriggerRadiusM` (`int?`) và vị trí Boat (`decimal`).

Yêu cầu:

1. Dùng **NetTopologySuite đúng roadmap** để tính khoảng cách Boat–POI theo mét. Kinh/vĩ độ WGS84 phải được chiếu sang **cùng hệ tọa độ đơn vị mét** trước khi gọi `Distance`; không dùng NTS trực tiếp trên longitude/latitude (đơn vị độ). Lát cắt hiện dùng ProjNET chuyển sang UTM phù hợp cho hai điểm gần nhau trong geofence; không dùng phép đo này như khoảng cách toàn cầu.
2. Tạo hàm xác định Boat có nằm trong bán kính POI hay không. Input không hợp lệ/null phải có kết quả hoặc lỗi rõ ràng, không tự đặt bán kính mặc định.
3. Test điểm trùng nhau, khoảng cách đã biết, trong/ngoài/đúng biên, tọa độ không hợp lệ và `TriggerRadiusM = null`.
4. Không đọc DB, không phát SignalR event, không tạo trạng thái chống phát lặp trong lát cắt này.
5. Báo cáo công thức, đơn vị, tolerance test và các file thay đổi.

**Kết quả nghiệm thu lát cắt:** core có test chạy được và có API nội bộ rõ ràng. **Đây mới là bước 1 trong việc Thịnh được giao**: sau khi có contract tracking/player, Thịnh còn phải xét Trip/POI/audio, chống phát lặp và phát lệnh trigger audio đúng POI. Khoảng cách tới bến và ETA do phần khác của nhóm làm.

### Prompt giao Antigravity cho lát cắt 1

```text
Đọc docs/thinh-ai/README.md, DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md,
PROJECT_CONTEXT_AND_RULES.md, IMPLEMENT_NOW.md (lát cắt 1),
P3_BE_02_GEOFENCE.md, INTEGRATION_LATER.md và DELIVERY_WORKFLOW.md;
đọc BR-EXP-01..07, BR-TRK-01 và entity PointOfInterest, RoutePoi,
BoatPositionEvent trong source.

Chỉ triển khai geofence core thuần cho Boat–POI theo lát cắt 1.
Không làm CRUD POI, ETA, khoảng cách tới bến; không sửa schema,
tracking hub, controller, TTS, UI hoặc booking.
Trước khi sửa, nêu file định tạo/sửa và cách chiếu WGS84 sang mét cho NTS. Sau khi sửa,
chạy build/test backend, tự xem diff và báo điều gì còn chờ tích hợp.
Không tuyên bố đã hoàn tất P3-BE-02.
Không tuyên bố đã xong phần geofence của Thịnh khi mới có hàm tính khoảng cách mà chưa có logic trigger audio.
```

## Lát cắt 2 — Azure TTS adapter sinh MP3

**Giao sau lát cắt 1 hoặc song song trên branch tách biệt.** Drive giao Thịnh Azure AI Text-to-Speech Neural Voice sinh MP3 tiếng Việt từ kịch bản. BR FINAL yêu cầu audio được chuẩn bị/kiểm duyệt và phát qua loa Boat.

Yêu cầu:

1. Tạo interface ở Application nhận text tiếng Việt và cancellation token, trả byte/stream MP3 cùng metadata cần thiết. Adapter Azure đặt ở Infrastructure.
2. Region, voice, credential, output format và timeout đọc từ cấu hình; không hard-code secret. Mặc định voice/region **chưa được nhóm chốt**: yêu cầu cấu hình rõ ràng và báo thiếu cấu hình ở runtime.
3. Unit test adapter/application contract với fake, gồm input rỗng, lỗi provider, cancellation; nếu cần live smoke test, chỉ chạy khi có credential dev riêng.
4. Không ghi DB, không lưu file vào local path coi như production URI, không thêm `ScriptText` hoặc sửa `AudioGuide` trong lát cắt này.
5. Nêu rõ MP3 output có thể dùng bởi storage adapter tương lai; file audio thật trong DoD `P3-BE-03` vẫn cần storage và thử nghiệm provider thật.

**Kết quả nghiệm thu lát cắt:** có adapter TTS cấu hình được và test được, chưa đánh dấu `P3-BE-03` hoàn tất.

### Prompt giao Antigravity cho lát cắt 2

```text
Đọc docs/thinh-ai/README.md, DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md,
PROJECT_CONTEXT_AND_RULES.md, IMPLEMENT_NOW.md (lát cắt 2),
P3_BE_03_POI_AZURE_TTS.md, INTEGRATION_LATER.md và DELIVERY_WORKFLOW.md;
đọc BR-EXP-02..07, entity AudioGuide và DI/package pattern hiện có.

Chỉ triển khai interface + Azure TTS adapter sinh MP3 tiếng Việt có
cấu hình và test. Không tạo CRUD POI, migration, audio storage, endpoint,
playback, UI hay geofence. Trước khi sửa, nêu package/API định dùng,
file scope và cách test không cần credential. Sau khi sửa, chạy build/test,
tự xem diff và báo các cấu hình còn thiếu cho live test.
Không tuyên bố đã hoàn tất P3-BE-03.
```

## Lát cắt 3 — AI Booking Assistant contract và tool boundary

Thịnh đã được Drive giao AI Booking Assistant, nhưng repo xếp widget `P4-WEB-03` là optional và phụ thuộc Trip Search + Booking ổn định. Hiện `SearchTripsQuery` có `BasePrice = 0` (`TODO`) và suy bến dừng bằng `Station.OrderIndex` thay vì stop calls thực tế. Vì vậy chưa được dùng kết quả hiện tại để khẳng định giá/bến hợp lệ trong câu trả lời AI.

**Giao được trên branch riêng, sau khi lát cắt 1/2 hoặc song song khi không đụng file nhau:** định nghĩa request/response typed cho AI chat và Trip Card; interface cho search tool và booking-link tool; validation tham số; fake adapters và tests. Thịnh đã chọn OpenAI tại [AI_DECISIONS.md](AI_DECISIONS.md), nhưng lát cắt này chưa cần nối LLM hoặc API hiện tại như dữ liệu production. Đây là phần đầu của trách nhiệm AI Booking Assistant, không phải toàn bộ chatbot.

### Prompt giao Antigravity cho lát cắt 3

```text
Đọc docs/thinh-ai/README.md, FULL_SCOPE_EXECUTION_PLAN.md,
DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md, PROJECT_CONTEXT_AND_RULES.md,
IMPLEMENT_NOW.md (lát cắt 3), P4_AI_BOOKING_ASSISTANT.md,
INTEGRATION_LATER.md và DELIVERY_WORKFLOW.md; đọc BR-AI-01.

Chỉ thiết kế/triển khai contract typed và boundary cho AI Booking Assistant:
request chat, TripCard response, search tool và booking-link tool qua interface,
validation và test với fake. Không thêm UI, sửa SearchTripsQuery/Booking,
không tự tạo URL/giá, không thêm provider LLM trong lát cắt contract này; lựa chọn OpenAI đã ghi tại AI_DECISIONS.md.
Trước khi sửa, báo file scope và các field dữ liệu cần owner Trip Search/Web chốt.
Sau khi sửa, chạy build/test, tự xem diff và báo phần còn chờ tích hợp.
Không tuyên bố đã hoàn tất AI Booking Assistant.
```

Để hoàn thành AI Assistant sau đó phải làm đủ Semantic Kernel + OpenAI thật + function calling API tìm chuyến + Trip Card trên UI theo [FULL_SCOPE_EXECUTION_PLAN.md](FULL_SCOPE_EXECUTION_PLAN.md). Lát cắt 1, 2 hoặc 3 không cần credential; live adapter/test cần key/quota hợp lệ.
