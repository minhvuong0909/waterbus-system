# Đối chiếu nguồn chính thức cho phần POI, audio, geofence và AI

Rà soát ngày 09/10/2026 trên repo hiện tại. Bảng này phân biệt **quyết định đã ghi trong repo**, **trạng thái source**, và **đề xuất của bộ docs thinh-ai**. Không dùng đề xuất như quyết định thay nhóm.

| Kết luận | Nguồn trong repo | Mức chắc chắn / hành động |
|---|---|---|
| Passenger và Captain dùng Web, mobile chỉ có Staff Scanner | `Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md` `BR-SCOPE-02` | BR đã chốt. Các dòng `P3-MOB-01..04` trong roadmap còn mô tả Captain/Passenger Mobile là dấu vết cũ, không triển khai theo đó. |
| Sightseeing có Route riêng, POI và audio dựa trên vị trí Boat | BR `BR-EXP-01`, `BR-TRK-01` | BR đã chốt. Không dùng GPS Passenger. |
| Audio chỉ tiếng Việt, phát chung qua loa Boat | BR `BR-EXP-02..04`, `BR-ID-06`; `05_AUDIT_REPORT_VI.md` mục GPS/Sightseeing | BR đã chốt. Không làm personal audio/unlock theo Ticket. |
| Audio đã chuẩn bị/kiểm duyệt; Onboard Player thực sự phát | BR `BR-EXP-03`, `BR-EXP-06` | BR đã chốt. Backend có file/URI không đồng nghĩa loa đã phát. |
| Ngăn trigger trùng, dừng/nhường ưu tiên khi Suspended/Terminated hoặc có thông báo an toàn | BR `BR-EXP-05..07`, `BR-INC-05` | BR đã chốt. Thuật toán chống lặp cụ thể chưa chốt. |
| Admin quản lý nội dung POI/audio | BR `BR-CFG-01` | BR đã chốt; source có policy `AdminOnly` trong `Infrastructure/DependencyInjection.cs`. |
| `P3-BE-03` = CRUD POI + Azure TTS, không có dependency trong bảng roadmap; DoD là file audio khả dụng | `TASK_BREAKDOWN_ROADMAP.md` Phase 3 Backend | **Thịnh chỉ làm Azure TTS**; CRUD POI do phần khác của nhóm làm. Fake provider chỉ đủ cho test TTS. |
| `P3-BE-02` = Geofence + ETA dùng NetTopologySuite, khoảng cách POI/bến; phụ thuộc `P3-BE-01` | `TASK_BREAKDOWN_ROADMAP.md` Phase 3 Backend | **Thịnh chỉ làm geofence POI để kích hoạt audio**; ETA và khoảng cách bến nằm ngoài task của Thịnh. |
| UI quản lý POI/kịch bản là `P3-WEB-02` | `TASK_BREAKDOWN_ROADMAP.md` Phase 3 Web | Module Web riêng, cần thống nhất API/schema với owner. |
| AI widget `P4-WEB-03` là optional, phụ thuộc Trip Search + Booking, tạo link đặt vé | `TASK_BREAKDOWN_ROADMAP.md` Phase 4 Web; BR `BR-AI-01` | Module optional. Không có module backend AI riêng hoặc provider đã chốt trong repo. |
| Domain hiện có `PointOfInterest`, `RoutePoi`, `AudioGuide`, `BoatPositionEvent` | `Domain/Entities/*.cs`, `Infrastructure/Persistence/Configurations/EntityConfigurations.cs`, migrations | Trạng thái source. `RoutePoi.TriggerRadiusM` nullable; `AudioGuide` chưa có script text. |
| Logical ERD có các bảng tương ứng và unique `(route_id, display_order)`, `(poi_id, language_code)` | `02_Logical_ERD_REVIEWED.dbml` | Thiết kế reviewed, không phải giấy phép tự đổi schema. |
| Thịnh được giao AI Booking Assistant và AI Sightseeing Audio | Google Doc “Bản sao của Waterbus Management”, tab `module`, mục “Thành viên 5: AI Developer – Thịnh” | **Phân công ghi rõ trên Drive**: Semantic Kernel + OpenAI/Gemini + API tìm chuyến/UI Card; Azure TTS sinh MP3 tiếng Việt + logic geofence kích hoạt audio ở POI. |
| Repo không gán tên cho module ID | Không có “Thịnh/Thinh” trong roadmap/task repo | Repo cung cấp module/dependency/DoD. Thịnh đã xác nhận không nhận CRUD POI và ETA; không giao Antigravity hai phần đó. |

## Các chỗ còn phải chốt trước khi tích hợp nhóm

1. **Ranh giới module:** Thịnh làm TTS/geofence POI và AI Booking Assistant. CRUD POI, ETA, frontend widget và `P3-BE-01` do phần khác của nhóm làm; chỉ cần thống nhất contract khi tích hợp.
2. **Kịch bản audio:** `P3-WEB-02` yêu cầu UI quản lý kịch bản nhưng entity hiện không có field lưu script. Cần thống nhất schema và API trước khi thêm migration.
3. **Audio storage/TTS:** BR FINAL chốt Onboard Player phát; Thịnh chọn Azure Blob cho MP3 trong `AI_DECISIONS.md`. Voice/format/region, tài khoản/quyền Blob và giao thức player vẫn cần chốt với owner.
4. **Geofence POI:** input tracking, thuật toán chống phát lặp và `TriggerRadiusM` nullable chưa có contract cụ thể. ETA không nằm trong task Thịnh.
5. **AI Assistant:** `P4-WEB-03` optional; Thịnh chọn OpenAI `gpt-4.1-mini`, còn key/quota, payload Trip Card và thời điểm/owner frontend cần xác nhận.

## Lưu ý về độ mới của tài liệu repo

`TASK_BREAKDOWN_ROADMAP.md` và `BACKEND_TASK_BREAKDOWN.md` có snapshot source cũ (ví dụ nói chưa có migration hoặc chưa có frontend/mobile), trong khi checkout hiện tại đã có migrations, `waterbus-fe` và `waterbus-mobile`. Dùng chúng để lấy module/dependency/DoD, rồi kiểm tra lại source trước khi kết luận thứ gì đã hoặc chưa triển khai.
