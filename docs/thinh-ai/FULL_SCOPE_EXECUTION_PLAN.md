# Kế hoạch hoàn tất toàn bộ phần AI của Thịnh

Nguồn phân công: Google Doc “Bản sao của Waterbus Management”, tab `module`, mục “Thành viên 5: AI Developer – Thịnh”. Nguồn nghiệp vụ: BR v2.0 FINAL trong repo. Kế hoạch này bao phủ **cả hai đầu việc** của Thịnh, kể cả phần tích hợp chạy thật; [IMPLEMENT_NOW.md](IMPLEMENT_NOW.md) chỉ mô tả các bước code độc lập đầu tiên.

## 1. Kiểm kê yêu cầu để không bỏ sót

| Câu trong phân công | Kết quả Thịnh phải bàn giao | Điều kiện chứng minh hoàn thành |
|---|---|---|
| “Tích hợp Semantic Kernel kết nối với OpenAI/Gemini” | AI Booking Assistant chạy qua Semantic Kernel; Thịnh hiện chọn **Groq**, khác hai provider được giao, cần nhóm chấp nhận thay đổi | Gọi provider thật từ backend bằng key cấu hình ngoài Git; lỗi/quota/timeout có xử lý và test. Nếu nhóm không chấp nhận Groq, cần quay về provider được giao. |
| “Khai báo các Plugins/Functions để AI tự hiểu ý định khách hàng” | Tool/function có schema rõ, validate tham số tìm chuyến | Các câu hỏi về ngày, bến, loại dịch vụ được phân tích đúng; thiếu dữ liệu thì hỏi lại, không đoán. |
| “Gọi API tìm chuyến” | Tool gọi API/query tìm chuyến đã được owner xác nhận, qua quyền hợp lệ | Kết quả chat dùng chuyến/giờ từ API thật; không bịa giá, ghế hoặc bến. |
| “Hiển thị UI Card” | Backend trả **dữ liệu Trip Card có cấu trúc** cho Viễn; Viễn render UI | Chat trên Customer Web hiển thị card đúng chuyến và link đặt vé hợp lệ. Thịnh không tự làm chat widget/UI. |
| “Azure AI Text-to-Speech (Neural Voice) sinh file .mp3 tiếng Việt từ kịch bản Text” | Adapter Azure Speech tạo MP3 tiếng Việt từ kịch bản của nhóm; file được lưu/duyệt theo contract | Live test Azure thật; MP3 phát được, URI bền vững tới Onboard Player, audio `vi` đã duyệt. |
| “Code logic đo khoảng cách Geofence” | Tính khoảng cách Boat–POI theo mét, kiểm tra bán kính/Trip/POI/audio hợp lệ | Test biên và vị trí mẫu; không kích hoạt Regular/Trip không hợp lệ. |
| “Để kích hoạt phát audio khi tàu đến đúng tọa độ địa danh” | **Logic quyết định và gửi trigger đúng POI**, chống lặp, nhường thông báo an toàn | Boat vào POI → một trigger → thiết bị trên tàu phát đúng MP3; GPS lặp không phát lại; an toàn ngắt audio. |

## 2. Phạm vi và ranh giới nhóm

Thịnh làm: AI tool-calling/backend response, Azure TTS, geofence Boat–POI **đến lệnh trigger audio** và các adapter tích hợp của ba phần đó.

Thịnh không làm: CRUD POI/RoutePOI, quản lý kịch bản UI, ETA/khoảng cách tới bến, BoatTrackingHub/GPS ingestion, Customer Web chat widget/Trip Card rendering, Booking engine, audio player/loa. Việc người khác làm là dependency, không phải lý do bỏ yêu cầu tích hợp ở bảng trên.

## 3. Thứ tự thực hiện

### Giai đoạn A — Làm độc lập ngay

1. **Geofence core:** khoảng cách địa lý mét và điều kiện bán kính POI, unit tests. Prompt trong [IMPLEMENT_NOW.md](IMPLEMENT_NOW.md), lát cắt 1.
2. **Azure TTS adapter:** interface Application, Azure Speech adapter Infrastructure, MP3 tiếng Việt, cấu hình và tests. Prompt trong `IMPLEMENT_NOW.md`, lát cắt 2.
3. **AI Assistant contract:** thiết kế request tìm chuyến, tool schema và response Trip Card độc lập model; test fake tool/model. Không nối API hiện tại để khẳng định giá/bến vì `SearchTripsQuery` còn `BasePrice = 0` và suy stops theo giả định cũ. Việc này thuộc Thịnh nhưng phải giữ trong branch/PR riêng và theo [P4_AI_BOOKING_ASSISTANT.md](P4_AI_BOOKING_ASSISTANT.md).

Kết quả A là code/test có thể review, **chưa hoàn thành phân công**.

### Giai đoạn B — Chốt contract với nhóm, không sửa hộ module của họ

1. Owner POI/kịch bản cung cấp nguồn script, POI active, RoutePoi radius, AudioGuide publish/approval và nơi lưu MP3.
2. Kiệt/owner tracking cung cấp position event của Boat/Trip; owner Onboard Player cung cấp lệnh phát, ACK và safety override.
3. Owner Trip Search/Booking cung cấp search result chính xác và cách tạo booking link; Viễn chốt Trip Card JSON và hiển thị chat.
4. Thịnh đã chọn Groq và Azure Blob; nhóm xác nhận đổi provider AI so với Drive và quyền dùng hạ tầng/credential, Azure voice/region, môi trường demo/chạy thật theo [AI_DECISIONS.md](AI_DECISIONS.md).

Các câu hỏi và contract cần ghi vào [INTEGRATION_LATER.md](INTEGRATION_LATER.md). Không tự thêm schema, hub hoặc UI để lấp chỗ chưa có.

### Giai đoạn C — Hoàn thành ba luồng được giao

1. **TTS end-to-end:** script đã duyệt → Azure Neural Voice → MP3 → storage → AudioGuide `vi`/publish theo contract → player lấy đúng file.
2. **Geofence trigger end-to-end:** vị trí Boat từ tracking → POI của đúng Sightseeing Trip → khoảng cách/bán kính → chống lặp/trạng thái an toàn → lệnh trigger tới Onboard Player → ACK/quan sát kết quả. Không cần làm ETA.
3. **AI Assistant end-to-end:** câu hỏi khách → Semantic Kernel/provider → tool tìm chuyến qua API thật → response Trip Card có cấu trúc → widget của Viễn render → link mở đúng luồng Booking. Không cho AI tự ghi Payment/Booking hoặc vượt quyền (`BR-AI-01`).

## 4. Điều kiện nghiệm thu cuối cùng

- Cả **7 dòng yêu cầu** trong bảng mục 1 đều có code/contract và bằng chứng test tương ứng.
- Azure và LLM provider thật chạy bằng secret ngoài Git; fake chỉ dùng cho automated tests.
- Trip Sightseeing thực hoặc dữ liệu mô phỏng vị trí Boat theo BR đi qua POI, phát đúng MP3 tiếng Việt trên loa; Regular không phát; GPS lặp không trigger trùng; Suspended/Terminated/cảnh báo an toàn dừng/ưu tiên đúng.
- AI trả giờ tàu và Trip Card từ API đã sửa/duyệt; link đặt vé dùng được. Thiếu dữ liệu thì hỏi lại hoặc báo không biết.
- Build/test backend pass, UI integration test và demo xuyên tầng pass. PR chỉ thay file thuộc phần Thịnh hoặc adapter đã được owner đồng ý.
- Không tuyên bố hoàn tất chỉ vì giai đoạn A xong; xem thêm checklist chạy thật tại [INTEGRATION_LATER.md](INTEGRATION_LATER.md).

## 5. Quyết định còn cần chốt để chạy thật

**Đã rõ ba điểm:** Thịnh chọn Groq `openai/gpt-oss-20b` (cần nhóm xác nhận vì Drive nêu OpenAI/Gemini), MP3 trên Azure Blob; BR FINAL đã chốt Onboard Player phát ra loa. Xem [AI_DECISIONS.md](AI_DECISIONS.md). **Vẫn cần team cung cấp:** nguồn/version kịch bản, Azure/Groq credentials và quyền dùng Blob; contract gửi trigger/ACK với player và tracking; Trip Card/booking link cùng API Trip Search đã sửa. Thiếu các contract đó chặn phần tương ứng của giai đoạn C, **không chặn giai đoạn A**. CRUD POI và ETA vẫn ngoài scope Thịnh trong mọi trường hợp.
