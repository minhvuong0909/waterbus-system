# Chuyển phần AI/audio sang service layer — 29/10/2026

Phạm vi: `AiBooking` và `SightseeingAudio`, theo `AGENTS.md`, `docs/CONVENTIONS.md` mục 3.2, mẫu Auth và phần thương mại trong PR #39. Không đổi nghiệp vụ, route HTTP, JSON payload, entity hoặc schema database.

## Vị trí file và tên mới

Các đường dẫn dưới đây tính từ `waterbus-be/src/`; `Application` và `Infrastructure` là cách viết gọn của `WaterbusSystem.Application` và `WaterbusSystem.Infrastructure`.

| Trước | Sau | Trách nhiệm |
|---|---|---|
| `SynthesizeAudioCommand`/Handler | `Application/DTOs/Requests/SightseeingAudio/SynthesizeAudioRequest.cs`, `Application/Services/SightseeingAudio/AudioSynthesisService.cs` | Gọi Azure TTS rồi lưu MP3 qua storage interface |
| `GetAudioAssetQuery`/Handler | `GetAudioAssetRequest`, `AudioAssetService` trong các thư mục Request/Service tương ứng | Tải MP3 private để Admin nghe |
| `PublishAudioGuideCommand`/Handler | `PublishAudioGuideRequest`, `AudioGuideService` | Kiểm tra POI, MP3 tồn tại, tạo/cập nhật AudioGuide và publish |
| `GetAudioGuidePoisQuery`/Handler | `GetAudioGuidePoisRequest`, `AudioGuideService` | Đọc POI/audio có phân trang |
| `AskAiBookingQuery`/Handler | `Application/Interfaces/Services/IAiBookingAssistantService.cs` | Controller gọi trực tiếp `ProcessChatAsync`; bỏ lớp chuyển tiếp MediatR |
| `StoreAudioCommand` | `Application/DTOs/Requests/SightseeingAudio/StoreAudioRequest.cs` | Request nội bộ của storage, không phải command CQRS |

- HTTP Request/Response: `Application/DTOs/Requests|Responses/AiBooking|SightseeingAudio/`.
- Validator: `Application/Validators/AiBooking|SightseeingAudio/`.
- Service interface: `Application/Interfaces/Services/`.
- Service audio: `Application/Services/SightseeingAudio/`; gồm core geofence, evaluator tracking và tạo asset.
- Truy cập SQL audio: `Application/Interfaces/Repositories/ISightseeingAudioRepository.cs` → `Infrastructure/Repositories/SightseeingAudioRepository.cs`.
- Ghi AudioGuide dùng `IUnitOfWork`/`EfUnitOfWork` chung; không thêm transaction engine hoặc DB migration.
- Service AI dùng Semantic Kernel và provider Azure vẫn ở `Infrastructure/Services/` vì cần SDK hạ tầng.
- Models và interface tích hợp nội bộ còn ở `Application/Features/AiBooking|SightseeingAudio/`; không có `IRequest`/Handler MediatR trong hai feature này.

## Luồng sau khi chuyển

Audio HTTP: Controller → `ValidateRequestFilter` → `IAudioSynthesisService`/`IAudioAssetService`/`IAudioGuideService` → provider Azure hoặc repository → SQL. Service cũng validate khi được gọi trực tiếp. Blob client khởi tạo lúc truy cập asset, nên đọc danh sách POI không cần cấu hình Azure.

Geofence: tracking gọi `ITrackingAudioTriggerEvaluator` → repository đọc snapshot Trip/RoutePoi/AudioGuide/Incident → `IPoiAudioTriggerDecisionService` áp dụng điều kiện hành trình, bán kính, ưu tiên an toàn và chống lặp → dispatcher hiện có. Không thay đổi thuật toán NetTopologySuite hoặc cơ chế ACK/player.

AI chat: `AiAssistantController` → `IAiBookingAssistantService` → `SemanticKernelAiBookingAssistantService` → plugin/tool. Request, lịch sử, search context và dữ liệu Trip Card giữ nguyên.

**Ranh giới với teammate:** `Trips` và `Stations` chưa migrate. `MediatRTripSearchTool` vẫn gọi `SearchTripsQuery`/`GetStationsQuery` qua `ISender` trong Infrastructure để tái sử dụng đúng nghiệp vụ của hai module đó. Đây là adapter tích hợp; không còn CQRS cho use case chat. Khi owner chuyển Trips/Stations, cập nhật adapter theo service interface họ cung cấp. Không tự viết lại truy vấn tìm chuyến hoặc bến.

## API và file dùng chung

Giữ các route: `POST /api/v1/AiAssistant/chat`, `POST /api/v1/TextToSpeech/generate`, `GET /api/v1/TextToSpeech/audio?assetKey=...`, `GET /api/v1/AudioGuides/pois?page=1&pageSize=20`, `POST /api/v1/AudioGuides/publish`. JSON camelCase giữ nguyên; schema C# của audio đổi hậu tố `Dto` sang `Response` và input đổi sang `Request` trên Swagger.

File dùng chung có thay đổi: DI Application/Infrastructure đăng ký service/repository; `RecordBoatPositionCommand.cs` và test tracking chỉ cập nhật `using` cho interface audio đã chuyển thư mục. Không sửa logic Tracking, Trips, Stations, Booking, Payment, Refund hoặc FE. Bảng tiến độ trong `AGENTS.md`/`CONVENTIONS.md` được cập nhật theo yêu cầu migrate của repo.

## Kiểm chứng

Các test handler được chuyển thành `AudioSynthesisServiceTests`, `AudioAssetServiceTests`, `AudioGuideServiceTests`; giữ các trường hợp cũ. Test geofence đọc qua repository mới. `AiModuleArchitectureTests` ngăn Application AI/audio phụ thuộc MediatR/DbContext; `AiModuleHttpTests` kiểm tra route, binding, JWT Admin/Passenger, validation ProblemDetails, tải/publish audio và response chat. Provider giả trong HTTP/unit test chỉ nằm trong test assembly.

```powershell
cd waterbus-be
dotnet build WaterbusSystem.sln -c Release
dotnet test WaterbusSystem.sln -c Release --no-build
```

TTS/Blob và Groq thật vẫn cần credential local. Hướng dẫn API audio: [LOCAL_AUDIO_INTEGRATION_STATUS.md](LOCAL_AUDIO_INTEGRATION_STATUS.md). Việc đổi kiến trúc không bổ sung Onboard Player: dispatcher log-only và dedup in-memory vẫn có giới hạn đã ghi ở tài liệu đó.

Kết quả kiểm chứng ngày 29/10/2026:

- Release build: 0 warning, 0 error. Toàn bộ test backend: 784 pass, 18 skip, 0 fail; các test opt-in không được tính là đã kiểm chứng live.
- API local với SQL Server Docker và Azure thật: đọc 5 POI, tạo MP3 15.840 byte, tải lại cùng kích thước, publish và gọi lặp giữ nguyên AudioGuideId; đọc lại API xác nhận URI và trạng thái publish trong SQL.
- AI chat qua HTTP đã đi tới Groq thật nhưng provider trả `401 invalid_api_key`; API trả fallback theo xử lý lỗi hiện có. Chưa xác nhận hội thoại LLM thành công sau migration cho tới khi cấu hình key hợp lệ. HTTP contract và điều phối Semantic Kernel đã được kiểm tra tự động bằng test provider.
