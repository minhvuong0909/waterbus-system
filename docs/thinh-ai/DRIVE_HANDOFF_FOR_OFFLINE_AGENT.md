# Bàn giao nội dung Drive cho coding agent không có quyền truy cập Drive

Tài liệu này tóm tắt **những thông tin liên quan đến phần AI của Thịnh** từ Google Doc “Bản sao của Waterbus Management” (các tab `module`, `document`, `skill`), kiểm tra ngày 09/10/2026. Đây là bản bàn giao để Antigravity làm việc chỉ với repo. Nó không thay thế BR FINAL và không biến các ví dụ trong Drive thành API contract.

## 1. Phân công ghi rõ trên tab `module`

**Thành viên 5: AI Developer – Thịnh**:

1. **AI Booking Assistant:** Semantic Kernel kết nối OpenAI/Gemini; khai báo plugin/function để hiểu ý định khách, gọi API tìm chuyến và trả dữ liệu cho UI Card.
2. **AI Sightseeing Audio:** Azure AI Text-to-Speech Neural Voice tạo file MP3 tiếng Việt từ kịch bản; đo khoảng cách geofence để kích hoạt audio khi Boat đến tọa độ POI.

Các phần phụ thuộc do tab này giao cho người khác:

- Viễn làm Customer Web UI, gồm chat widget và render Trip Card từ phản hồi AI.
- Kiệt làm SignalR hubs, gồm BoatTrackingHub. Thịnh cần contract vị trí tàu để làm geofence.
- Thịnh đã xác nhận **không nhận CRUD POI, ETA hoặc Web admin UI**. Roadmap có các phần đó nhưng chúng không nằm trong task giao Antigravity của Thịnh.

## 2. Ý định sản phẩm trên tab `document`

### AI Booking Assistant

- Chat widget trên Customer Web giúp khách hỏi giờ tàu chạy và tìm chuyến bằng ngôn ngữ tự nhiên.
- Drive nêu ví dụ tư vấn khung giờ ngắm hoàng hôn và Function Calling tạo link đặt vé nhanh.
- UI frontend có khả năng render Trip Card từ dữ liệu AI trả về.
- Giá, giờ, ghế, địa điểm và khung giờ hoàng hôn phải lấy từ nguồn dữ liệu đáng tin hoặc nêu là chưa có dữ liệu; không để model tự bịa. Repo chỉ chốt DoD `P4-WEB-03`: trả lời đúng giờ tàu và tạo được link đặt vé.

### Sightseeing Audio

- Chuyến Sightseeing phát nội dung thuyết minh khi Boat vào vùng geofence của POI; chuyến Regular không phát audio tham quan.
- Azure Speech tạo file audio trước khi phát; Onboard Player/thiết bị trên Boat là nơi phát ra loa.
- Drive mô tả thao tác Pause/Replay và ưu tiên thông báo an toàn. BR FINAL bắt buộc audio nhường ưu tiên khi Suspended/Terminated hoặc có thông báo an toàn (`BR-EXP-06`).
- Drive đưa ví dụ SignalR event `TriggerPOI` với PoiId, AudioUrl, Language=`vi`, AutoPlay. Đây là **ví dụ định dạng**, chưa có contract/hub/event tương ứng được chốt trong repo; thống nhất với owner BoatTrackingHub và Onboard Player trước khi code.
- Drive nêu NetTopologySuite cho khoảng cách/ETA; roadmap repo gộp geofence và ETA trong `P3-BE-02`. **Thịnh chỉ làm geofence kích hoạt audio tại POI**, không làm ETA hoặc khoảng cách tới bến.

## 3. Nội dung tab `skill`

Tab `skill` là prompt trợ lý dự án, **không phải file hướng dẫn bắt buộc cho Antigravity**. Các ý phù hợp đã có nguồn mạnh hơn trong repo:

- Clean Architecture: Domain → Application → Infrastructure → WebApi.
- Semantic Kernel/Azure Speech là định hướng công nghệ, nhưng provider/model/config cụ thể vẫn phải chốt với nhóm.
- AI chỉ gọi function/API phù hợp, không được vượt phân quyền hoặc tự ý ghi nghiệp vụ (`BR-AI-01`).
- Audio hiện chỉ tiếng Việt, phát qua loa Boat (`BR-EXP-02..04`).

Tab `skill` gọi phần schema là “Physical Database Schema” và liệt kê tên bảng rút gọn. Khi cần schema, đọc `docs/02_Logical_ERD_REVIEWED.dbml`, entity, EF configuration và migration trong repo. Không sao chép prompt này làm hệ thống rule mới.

## 4. Những đoạn Drive đã bị BR FINAL thay thế

Không triển khai theo các mô tả sau trong tab `document`/roadmap cũ:

- Passenger/Captain mobile app riêng; `BR-SCOPE-02` chốt Passenger/Captain dùng Web, mobile chỉ dành cho Staff Scanner.
- Thuyết minh cá nhân đa ngôn ngữ qua điện thoại/tai nghe; `BR-EXP-02..04` chốt một ngôn ngữ tiếng Việt, loa chung trên Boat.
- QR động TOTP theo Ticket; `BR-QR-01..03` chốt QR tĩnh ký số theo Booking.
- Voucher/Wallet bồi thường; `BR-RF-05` loại khỏi scope hiện tại.
- Dispatcher là role riêng; `BR-ROLE-01` chốt Admin làm điều phối.

## 5. Cách dùng bản bàn giao

Agent chỉ cần đọc file này để biết **Drive đã giao gì và ý định sản phẩm là gì**. Sau đó đọc BR FINAL, roadmap, Logical ERD, `SETUP.md`, module spec và source trong repo. Nếu cần đổi schema hoặc API dùng chung, báo rõ quyết định còn thiếu để nhóm chốt với owner liên quan.
