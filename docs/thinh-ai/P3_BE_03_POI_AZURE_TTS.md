# Phần Thịnh trong P3-BE-03 — Azure TTS tiếng Việt

Roadmap repo gộp `CRUD POI + Azure TTS` thành `P3-BE-03`. **Thịnh chỉ làm phần Azure TTS** theo phân công Drive và xác nhận scope của Thịnh. CRUD POI, gắn POI vào Route và UI quản lý kịch bản **không thuộc task này**. Không dùng tên `P3-BE-03` để tự nhận toàn bộ module.

## Mục tiêu của Thịnh

- Nhận kịch bản tiếng Việt đã được tạo/duyệt từ phần nội dung của nhóm.
- Dùng Azure AI Speech Neural Voice tạo MP3 tiếng Việt.
- Trả file/stream cùng metadata đủ để phần audio storage của nhóm lưu bền vững.
- Khi tích hợp, bảo đảm audio được duyệt mới có thể publish cho Onboard Player; không coi backend là thiết bị trực tiếp phát ra loa (`BR-EXP-02..06`).

## Làm độc lập ngay

- Application định nghĩa interface TTS nhận text, cancellation token và tùy chọn cần thiết, trả MP3 + content type/duration khi provider hỗ trợ.
- Infrastructure implement Azure Speech adapter; cấu hình region/voice/output format/key qua Options, User Secrets hoặc environment.
- Validate input, timeout, cancellation và lỗi provider; không log key hoặc toàn bộ nội dung nhạy cảm.
- Có tests với fake/provider stub; test không phụ thuộc Azure credential.
- Có hướng dẫn live smoke test với credential dev riêng để chứng minh MP3 phát được.

Prompt giao agent và giới hạn file nằm trong [IMPLEMENT_NOW.md](IMPLEMENT_NOW.md), lát cắt 2.

## Chỉ làm khi contract tích hợp đã chốt

- Nguồn lưu kịch bản và version/approval do owner nội dung POI cung cấp. `PointOfInterest` và `AudioGuide` hiện **không có `ScriptText`**; Thịnh không tự thêm field/migration.
- Thịnh chọn **Azure Blob Storage** cho MP3 theo [AI_DECISIONS.md](AI_DECISIONS.md); owner hạ tầng xác nhận account/container/quyền. `AudioAssetUri` giữ URI/định danh bền vững, không chứa SAS URL hết hạn hoặc local temp path; cơ chế player truy cập file cần chốt với owner player.
- Liên kết với `AudioGuide(PoiId, LanguageCode = "vi")` theo contract CRUD/audio của nhóm. Source có unique index `(PoiId, LanguageCode)`; không tạo bản ghi trùng khi retry.
- Quy trình Admin duyệt nội dung và publish phải đúng `BR-EXP-03`, `BR-CFG-01`.

Các contract/owner cần trao đổi ghi tại [INTEGRATION_LATER.md](INTEGRATION_LATER.md).

## Không làm trong task này

- `GetPois`, `CreatePoi`, `UpdatePoi`, `DeletePoi`, `PoisController` hoặc API gắn POI vào Route.
- UI Admin POI/kịch bản.
- Geofence, ETA, BoatTrackingHub, Onboard Player hoặc Passenger audio.
- Audio đa ngôn ngữ, personal audio, Ticket-based unlock.

## Nghiệm thu phần TTS của Thịnh

1. Build/test backend pass; lỗi Azure/cancellation/input xấu được xử lý rõ ràng.
2. Live smoke test với Azure thật tạo MP3 tiếng Việt phát được; không commit credential hoặc file sinh ra.
3. Sau tích hợp, MP3 được lưu và được Onboard Player lấy đúng URI; audio chưa duyệt không publish. Đây là nghiệm thu tích hợp, không phải điều kiện để bắt đầu adapter.
4. PR chỉ chứa code TTS và adapter cần thiết; không có diff CRUD POI/ETA.
