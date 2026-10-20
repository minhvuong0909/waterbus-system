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

## Còn thiếu để chạy thật

1. Owner POI/Admin cung cấp luồng kịch bản đã duyệt → TTS → tạo/publish `AudioGuide` và dữ liệu RoutePoi hợp lệ. Hiện repo chưa có API/UI quản lý phần này; unit fixture tự tạo dữ liệu.
2. Owner tracking xác nhận nguồn tiến độ `NextRoutePoiDisplayOrder` và `VisitId` ổn định nếu tuyến có vùng POI chồng nhau/vòng lặp. Adapter hiện để null; core từ chối vùng chồng, không đoán POI gần nhất.
3. Owner Onboard Player chốt giao thức gửi lệnh, cấp quyền đọc Blob private (SAS ngắn hạn hoặc stream có xác thực), ACK đúng `RequestId`, hành vi dừng/nhường ưu tiên khi Captain override/thông báo an toàn. SOS Safety Open đã chặn lệnh mới ở backend nhưng **chưa** có tín hiệu override/audio đang phát từ thiết bị.
4. Thay dispatcher log-only và dedup in-memory bằng adapter production (idempotency qua nhiều instance/restart); kiểm thử loa tàu thật. Chưa có kiểm chứng playback end-to-end.

Do đó chưa thể coi ba việc trong kế hoạch cũ là “tích hợp chạy thật”. Đặc biệt REST trả URL MP3 hay FE player hành khách không được thêm chỉ để làm test vì Blob private và BR yêu cầu phát qua loa Boat.
