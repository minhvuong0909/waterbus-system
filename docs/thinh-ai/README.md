# Hướng dẫn triển khai phần việc của Thịnh

Hướng dẫn lấy và cấu hình key an toàn: [AI_KEYS_AND_LOCAL_TEST.md](AI_KEYS_AND_LOCAL_TEST.md).

Tài liệu này là bản hướng dẫn triển khai cho **phần AI của Thịnh**, đối chiếu Google Drive với repo tại ngày 09/10/2026. Tab `module` của Google Doc “Bản sao của Waterbus Management” ghi rõ **“Thành viên 5: AI Developer – Thịnh”**, gồm:

- **AI Booking Assistant:** Semantic Kernel kết nối OpenAI/Gemini, function/plugin gọi API tìm chuyến và trả dữ liệu cho UI Card.
- **AI Sightseeing Audio:** Azure AI Text-to-Speech sinh MP3 tiếng Việt từ kịch bản; **tính khoảng cách và quyết định trigger audio đúng POI** khi Boat tới vùng địa danh. Hàm tính khoảng cách thuần chỉ là bước đầu, chưa hoàn thành phần việc này.

Repo không ghi tên thành viên trong roadmap; repo dùng để xác định BR, kiến trúc và dependency. **Thịnh chỉ làm ba phần Drive giao: AI Booking Assistant, Azure TTS và geofence POI. Không giao Antigravity CRUD POI, ETA, UI chat/POI, BoatTrackingHub hoặc Onboard Player.** Các phần đó thuộc nhóm khác và chỉ là điểm tích hợp của Thịnh.

## 1. Có thể bắt đầu từ repo hiện tại không?

**Có, cho phần việc AI đã giao trên Drive.** Repo hiện tại đã có nền tảng:

- Có Business Rules v2.0 FINAL.
- Có Logical ERD đã review.
- Có `PointOfInterest`, `RoutePoi`, `AudioGuide`, `BoatPositionEvent`.
- Có EF Core mapping, DbSet và migrations.
- Có convention Clean Architecture, CQRS/MediatR, FluentValidation và unit test.

Google Drive hữu ích để biết phân công ban đầu, nhưng quyết định nghiệp vụ và ranh giới triển khai phải được kiểm tra lại với repo. Khi nguồn mâu thuẫn:

1. BR FINAL là baseline **nghiệp vụ**.
2. Logical ERD reviewed là **đề xuất thiết kế chi tiết**; audit của repo nói rõ không bắt buộc build cả 33 bảng cùng lúc.
3. Roadmap repo xác định **module, dependency và DoD**, nhưng có nội dung cũ trái BR FINAL và snapshot trạng thái code đã lỗi thời.
4. Source/migration cho biết **đang có gì**, không tự biến code hiện tại thành quyết định nghiệp vụ. Nếu code trái BR FINAL, báo nhóm trước khi xây tiếp trên chỗ trái đó.
5. Google Drive là nguồn **phân công ban đầu/ý tưởng**, cần xác nhận lại ở những chỗ xung đột với repo.

Không dùng bản `Smart_Waterbus_Business_Rules_Proposed_v1_0.docx` để ghi đè BR FINAL.

## 2. Thứ tự bắt buộc phải đọc

Đọc đúng thứ tự sau trước khi giao việc cho coding agent:

1. [FULL_SCOPE_EXECUTION_PLAN.md](FULL_SCOPE_EXECUTION_PLAN.md) — kế hoạch bao phủ toàn bộ phân công, kể cả tích hợp và nghiệm thu cuối.
2. [PROJECT_CONTEXT_AND_RULES.md](PROJECT_CONTEXT_AND_RULES.md)
3. [DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md](DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md) — bản tóm tắt các tab Drive liên quan, để agent không cần truy cập Drive.
4. [AI_DECISIONS.md](AI_DECISIONS.md) — ba điểm kỹ thuật đã chốt cho phần Thịnh và những contract nhóm vẫn còn thiếu.
5. `docs/Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md`, tối thiểu các BR được liệt kê trong tài liệu trên.
6. `docs/TASK_BREAKDOWN_ROADMAP.md`, mục Phase 3, Phase 4 và mục nguyên tắc chia việc; ghi chú các dòng trái BR FINAL.
7. `docs/02_Logical_ERD_REVIEWED.dbml`, các bảng `PointOfInterest`, `RoutePOI`, `AudioGuide`, `BoatPositionEvent`, `Trip`, `Route`.
8. `docs/05_AUDIT_REPORT_VI.md`, nhất là ranh giới Conceptual/Logical/Physical và phần Sightseeing.
9. `SETUP.md` và `docs/BACKEND_TASK_BREAKDOWN.md` cho convention; kiểm tra lại mọi nhận định về trạng thái source vì hai file có thông tin cũ.
10. Source của các entity/feature/DI/controller hiện có và [P3_BE_03_POI_AZURE_TTS.md](P3_BE_03_POI_AZURE_TTS.md).
11. [P3_BE_02_GEOFENCE.md](P3_BE_02_GEOFENCE.md) khi chuẩn bị geofence; [P4_AI_BOOKING_ASSISTANT.md](P4_AI_BOOKING_ASSISTANT.md) khi chuẩn bị AI Assistant.
12. [DELIVERY_WORKFLOW.md](DELIVERY_WORKFLOW.md).

Để bắt đầu code độc lập ngay, xem [IMPLEMENT_NOW.md](IMPLEMENT_NOW.md). Khi module của các thành viên khác sẵn sàng, dùng [INTEGRATION_LATER.md](INTEGRATION_LATER.md) để nối và nghiệm thu end-to-end.

Khi cần kiểm chứng một kết luận hoặc trao đổi với nhóm, dùng [SOURCE_TRACEABILITY.md](SOURCE_TRACEABILITY.md).

## 3. Thứ tự triển khai

Chỉ bắt đầu module được người phân công giao. Các hậu tố `A/B` dưới đây là cách **đề xuất chia PR**, không phải ID trong roadmap chính thức.

1. Thịnh chuẩn bị contract **TTS/audio**; dùng POI/kịch bản do phần CRUD POI của nhóm cung cấp.
2. Triển khai Azure TTS và file MP3 tiếng Việt theo contract đã chốt.
3. Thịnh chuẩn bị logic **geofence kích hoạt audio tại POI**; tích hợp theo `P3-BE-01 BoatTrackingHub` khi contract có sẵn. Không tính ETA tới bến.
4. Thịnh chuẩn bị contract/tool boundary AI Booking Assistant ngay ở lát cắt 3; hiện Thịnh chọn Groq trong `AI_DECISIONS.md` (khác OpenAI/Gemini trên Drive, cần nhóm xác nhận). Khi có API key/quota và Trip Search/Booking link ổn định, hoàn thành Semantic Kernel, function calling và tích hợp Trip Card với Viễn. `P4-WEB-03` là widget optional trong roadmap, nhưng AI Assistant vẫn nằm trong phân công Drive của Thịnh.

## 4. Những việc không được tự ý làm

- Không đổi entity/migration chỉ để “tiện code” nếu chưa chứng minh schema hiện tại thiếu.
- Không tạo Passenger/Captain mobile audio.
- Không sinh audio đa ngôn ngữ trong scope hiện tại; `vi` là ngôn ngữ duy nhất.
- Không cho LLM tự ghi dữ liệu nghiệp vụ hay vượt API đã phân quyền (`BR-AI-01`). Việc chatbot có được gọi API tạo checkout hay không phải được nhóm chốt riêng.
- Không thêm Voucher/Wallet.
- Không commit API key, connection string hoặc file audio sinh ra.
- Không sửa module Booking/Payment/QR/SignalR thuộc thành viên khác nếu task không yêu cầu.
- Không dùng nội dung Drive như nguồn chuẩn khi trái BR FINAL.

## 5. Cách giao cho Antigravity

Dùng prompt nền sau, rồi chỉ định đúng **một lát cắt được giao** trong `IMPLEMENT_NOW.md` hoặc một bước tích hợp đã có contract. Không giao nguyên dòng `P3-BE-02`/`P3-BE-03` vì chúng chứa CRUD POI/ETA ngoài scope Thịnh:

```text
Bạn đang làm trong repo Smart Waterbus. Trước khi sửa code, đọc toàn bộ:
- docs/thinh-ai/README.md
- docs/thinh-ai/FULL_SCOPE_EXECUTION_PLAN.md
- docs/thinh-ai/PROJECT_CONTEXT_AND_RULES.md
- docs/thinh-ai/DRIVE_HANDOFF_FOR_OFFLINE_AGENT.md
- docs/thinh-ai/AI_DECISIONS.md
- docs/thinh-ai/IMPLEMENT_NOW.md
- docs/thinh-ai/INTEGRATION_LATER.md
- tài liệu module được giao
- docs/thinh-ai/DELIVERY_WORKFLOW.md

Chỉ thực hiện lát cắt <LÁT_CẮT> thuộc phần Thịnh: AI Booking Assistant, Azure TTS hoặc geofence trigger POI. Không làm CRUD POI, ETA hoặc sửa module người khác.
Phân biệt BR FINAL (nghiệp vụ), roadmap (module/dependency/DoD), Logical ERD (thiết kế đề xuất) và source (trạng thái hiện có). Khi chúng mâu thuẫn, nêu rõ bằng chứng và dừng phần phụ thuộc vào quyết định đó.
Trước khi code, báo lại: phạm vi, file dự kiến sửa, rule áp dụng, dependency và điểm chưa rõ. Các endpoint/interface trong docs/thinh-ai là đề xuất, không phải contract đã chốt.
Sau khi code, chạy build/test, tự review diff và báo kết quả theo checklist trong tài liệu.
```
