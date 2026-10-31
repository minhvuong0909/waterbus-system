# Tình trạng nối GPS → POI audio (18/10/2026)

BR v2.0 `BR-EXP-02..06` yêu cầu **loa trên Boat**, không phải player của hành khách trên web. Vì vậy `AudioPlayer.tsx` không tồn tại không phải khoảng trống cần khôi phục. `AudioAssetUri` trong DB là URI bền vững của Blob **private**, không phải URL MP3 mở trực tiếp trên browser; chưa tạo endpoint trả URL công khai.

## Đã nối được trong code

- GPS qua REST/Hub/simulator đi vào `RecordBoatPositionCommandHandler`. Chỉ sau khi vị trí được chấp nhận (không cũ, đúng Boat/Captain) và tracking đã lưu/cache/publish, handler mới gọi adapter audio nếu cấu hình `SightseeingAudio:EnableGpsTriggerEvaluation=true`.
- Adapter đọc Trip, RoutePoi, POI, AudioGuide tiếng Việt đã publish từ DB; core geofence áp dụng Trip Sightseeing/EnRoute, bán kính, chống lặp và quy tắc vùng chồng. SOS loại Safety còn Open của Trip chặn lệnh mới dù Trip vẫn EnRoute.
- DI dùng `LogOnlyAudioTriggerDispatcher` và dedup in-memory **chỉ để local demo**. Log-only không có ACK từ player, nên không báo đã phát; reservation được nhả để có thể thử lại ở lần GPS sau. In-memory không bền qua restart hoặc nhiều API instance.
- Mặc định **tắt**. Không có secret mới. `appsettings.example.json`, `.env.example` và `docker-compose.yml` đã thêm cấu hình opt-in.

## Cách kiểm tra local hiện tại

Từ `waterbus-be`:

Để demo trực quan cho leader, chạy **một test dùng đúng geofence service và khoảng cách NetTopologySuite**, in bốn tình huống với khoảng cách đo được, quyết định và lý do:

```powershell
dotnet test WaterbusSystem.sln --filter FullyQualifiedName~GeofenceDemoTests --logger "console;verbosity=detailed"
```

GPS/POI, `AudioGuide` và ACK player trong bài demo là fixture; không gọi Azure/SQL/loa tàu. ACK giả chỉ chứng minh service đã gửi lệnh, **không** chứng minh audio đã phát.

```powershell
dotnet build WaterbusSystem.sln
dotnet test WaterbusSystem.sln --filter "FullyQualifiedName~TrackingAudioTriggerEvaluatorTests|FullyQualifiedName~RecordBoatPositionCommandHandlerTests|FullyQualifiedName~LocalAudioAdaptersTests"
```

Các test này dùng EF InMemory và fake ACK để chứng minh quyết định/đường nối, **không** dùng loa tàu hay Azure Blob thật. Để xem log qua API local, đặt WebApi User Secret `SightseeingAudio:EnableGpsTriggerEvaluation=true`, khởi động API ở Development và cần sẵn dữ liệu SQL: Trip Sightseeing `EnRoute` gắn Boat/Route, RoutePoi có tọa độ và bán kính dương, AudioGuide `vi` đã publish với URI Blob bền vững. Gửi GPS đúng Boat bằng tài khoản Captain được gán. Log `Audio geofence ...` cho biết lý do; `dispatched=true` chỉ là lệnh đã ghi log, **không** phải playback. Không gửi GPS giả cho Trip production. Xóa User Secret/cấu hình sau demo để tránh log lặp mỗi tick.

## Màn Admin soạn và nghe thử thuyết minh (23/10/2026)

Web Admin có trang **Thuyết minh** (`/admin/audio-guides`): bản đồ các địa danh dọc sông, chọn một địa danh → nhập lời thuyết minh → chọn giọng → sinh MP3 bằng Azure và nghe thử.

| API (chỉ role Admin) | Mục đích |
|---|---|
| `POST /api/v1/texttospeech/generate` body `{ text, voiceName }` | Có sẵn. Sinh MP3, lưu Blob private, trả `assetKey`, `durationSeconds`, `voiceName`... |
| `GET /api/v1/texttospeech/audio?assetKey=audio-assets/...mp3` | **Mới.** Backend đọc MP3 từ Blob private và trả `audio/mpeg` để Admin nghe thử. 400 khi khóa ngoài `audio-assets/` hoặc không phải `.mp3`, 404 khi không có tệp. |
| `GET /api/v1/audioguides/pois?page=1&pageSize=20` | Liệt kê POI active từ DB, `PoiId`, RoutePoi hợp lệ và trạng thái AudioGuide `vi`; không dùng danh mục giả của FE. |
| `POST /api/v1/audioguides/publish` | Admin gắn URI MP3 đã tồn tại trong đúng container Blob với `PoiId`, tạo/cập nhật `AudioGuide` `vi` và publish. Không tạo POI, không chấp nhận URL có SAS hoặc URI ngoài Blob đã cấu hình. |

Giọng: nữ `vi-VN-HoaiMyNeural`, nam `vi-VN-NamMinhNeural`.

Giới hạn hiện tại:
- Trang FE vẫn dùng danh sách địa danh/tọa độ gần đúng ở `features/admin/audioGuide.ts`; chưa được nối với API POI thật. Để test không mock, dùng POI từ `GET /audioguides/pois` trên Swagger.
- URI MP3 có thể publish vào `AudioGuide` qua Swagger, nhưng **kịch bản text chưa được lưu** (entity chưa có cột kịch bản); trang FE hiện chỉ giữ bản nháp trong phiên, không tự gọi API publish.
- Đây là nghe thử cho Admin qua stream có xác thực, **không** phải URL công khai hay player cho hành khách. Phát tự động trên loa tàu vẫn cần các mục dưới đây.

### Kiểm tra luồng thật bằng Swagger (local Development)

1. Cấu hình Azure Speech + Azure Blob bằng WebApi User Secrets, SQL Server local có dữ liệu POI/RoutePoi, đăng nhập Admin và bấm **Authorize** trong Swagger. Không đưa key vào request hay commit.
2. `GET /api/v1/audioguides/pois`: chọn một `poiId` từ DB có `hasActiveRoutePoi=true`. URI `/audio/poi-XX.mp3` trong seed là dữ liệu demo, **không** phải MP3 Azure thật.
3. `POST /api/v1/texttospeech/generate` với `{ "text": "Đây là lời thuyết minh thử cho địa danh.", "voiceName": "vi-VN-HoaiMyNeural" }`; giữ `audioUrl`, `assetKey`, `durationSeconds` trả về. Azure Speech/Blob được gọi thật.
4. `GET /api/v1/texttospeech/audio?assetKey=...` để tải/nghe chính MP3 đó. Sau khi nghe và duyệt, `POST /api/v1/audioguides/publish` với `{ "poiId": "<GUID từ bước 2>", "audioAssetUri": "<audioUrl từ bước 3>", "durationSeconds": <số từ bước 3> }`.
5. Gọi lại `GET /api/v1/audioguides/pois` và kiểm tra POI đó có `isPublished=true`, `audioAssetUri` đúng URL Blob. Bước này xác nhận MP3 thật đã gắn POI trong SQL; **chưa** chứng minh loa tàu phát. Geofence chỉ đánh giá khi có Trip Sightseeing `EnRoute`, GPS hợp lệ và `SightseeingAudio:EnableGpsTriggerEvaluation=true`.

Đã kiểm chứng local ngày 27/10/2026 bằng API Development và SQL Server Docker: đăng nhập Admin, đọc 5 POI từ DB, tạo MP3 thật qua Azure Speech/Blob (30.096 byte), tải lại qua API (30.096 byte), publish cho POI **Bán đảo Thanh Đa & Bình Quới**, rồi đọc lại DB qua API thấy `isPublished=true` và URI khớp. Đây là kiểm chứng luồng tạo/lưu/gắn audio; chưa có xác nhận phát loa tàu.

## Còn thiếu để chạy thật

1. API Admin đã nối MP3 thật với `AudioGuide` qua POI từ DB. Vẫn cần owner POI/Admin chốt lưu/version kịch bản, quy trình duyệt, dữ liệu RoutePoi và nối trang FE với API này; chưa có CRUD POI hoặc tự động publish từ UI.
2. Owner tracking xác nhận nguồn tiến độ `NextRoutePoiDisplayOrder` và `VisitId` ổn định nếu tuyến có vùng POI chồng nhau/vòng lặp. Adapter hiện để null; core từ chối vùng chồng, không đoán POI gần nhất.
3. Owner Onboard Player chốt giao thức gửi lệnh, cấp quyền đọc Blob private (SAS ngắn hạn hoặc stream có xác thực), ACK đúng `RequestId`, hành vi dừng/nhường ưu tiên khi Captain override/thông báo an toàn. SOS Safety Open đã chặn lệnh mới ở backend nhưng **chưa** có tín hiệu override/audio đang phát từ thiết bị.
4. Thay dispatcher log-only và dedup in-memory bằng adapter production (idempotency qua nhiều instance/restart); kiểm thử loa tàu thật. Chưa có kiểm chứng playback end-to-end.

Do đó chưa thể coi ba việc trong kế hoạch cũ là “tích hợp chạy thật”. Đặc biệt REST trả URL MP3 hay FE player hành khách không được thêm chỉ để làm test vì Blob private và BR yêu cầu phát qua loa Boat.
