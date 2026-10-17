# Hợp đồng Realtime (SignalR) — Smart Waterbus

Tài liệu cho người làm **Admin Radar**, **Captain Portal**, **sơ đồ ghế** (Web). Nguồn code: `waterbus-be/src/WaterbusSystem.WebApi/Realtime/RealtimeContract.cs`.

- Thư viện client: `@microsoft/signalr` (npm).
- Đường dẫn tương đối `/hubs/...` (nginx proxy cùng origin, WebSocket). Khi chạy `npm run dev` thì trỏ tới API (vd `http://localhost:5000/hubs/...`) và thêm origin vào `Cors:AllowedOrigins`.
- JWT gửi qua `accessTokenFactory` (thư viện tự đặt vào query `access_token`).
- Payload JSON **camelCase**, mọi payload có `version` (hiện là `1`). Thời gian là ISO-8601 UTC.
- **Hub chỉ để nhận dữ liệu realtime.** Sau khi kết nối lại (`onreconnected`), client phải **join lại group** và **gọi REST để lấy snapshot**, vì tin nhắn phát lúc mất kết nối không được gửi bù.

## 1. `/hubs/boat-tracking` — vị trí tàu & cảnh báo vận hành

Bắt buộc đăng nhập.

| Method (client gọi) | Quyền | Tác dụng |
|---|---|---|
| `JoinFleet()` / `LeaveFleet()` | Admin | Nhận vị trí **mọi** tàu, `weatherAlert`, `incidentRaised`, `tripStatusChanged` |
| `JoinBoat(boatId)` / `LeaveBoat(boatId)` | Đã đăng nhập | Nhận vị trí 1 tàu |
| `SendPosition(boatId, latitude, longitude, speedKnots, observedAt)` → `bool` | Captain **được gán cho tàu đó** (`Boat.CaptainUserId`) | Gửi GPS. `true` = đã nhận, `false` = bỏ qua vì cũ hơn vị trí đã có |

Lỗi trả về dạng `HubException` có thông điệp tiếng Việt (vd không phải thuyền trưởng của tàu, toạ độ sai phạm vi, thời điểm ở tương lai/quá cũ).

| Event (server đẩy) | Group | Payload |
|---|---|---|
| `boatPosition` | `fleet`, `boat:{boatId}` | `{ boatId, tripId?, lat, lng, speedKnots?, observedAt, version }` |
| `weatherAlert` | `fleet` | `{ incidentId, severity, routeId?, message, occurredAt, version }` |
| `incidentRaised` | `fleet` | `{ incidentId, incidentType, severity?, tripId?, boatId?, message, occurredAt, version }` — thuyền trưởng báo SOS |
| `tripStatusChanged` | `fleet`, `boat:{boatId}`; và `trip:{tripId}` trên hub ghế | `{ tripId, boatId?, status, previousStatus, operation, changedAt, version }` — `status`/`previousStatus` là tên TripStatus, `operation` là tên thao tác |

`tripId` chỉ có khi tàu đang có Trip ở trạng thái `Boarding`, `EnRoute` hoặc `Suspended` (BR-INC-05: chuyến tạm dừng vẫn phát vị trí).

### REST đi kèm

| API | Quyền | Mục đích |
|---|---|---|
| `GET /api/v1/tracking/boats` | Admin | Snapshot vị trí mới nhất các tàu (gọi khi mở radar và sau khi reconnect) |
| `GET /api/v1/tracking/boats/{boatId}/history?from=&to=&pageNumber=&pageSize=` | Admin | Lịch sử vị trí đã lưu (tối đa 7 ngày/lần, pageSize ≤ 500) |
| `POST /api/v1/tracking/boats/{boatId}/positions` | Captain | Gửi GPS khi không dùng được WebSocket. Body `{ latitude, longitude, speedKnots?, observedAt }` |

Lịch sử chỉ lưu thưa (mặc định 1 điểm / 15 giây / tàu, cấu hình `Tracking:PersistIntervalSeconds`); realtime vẫn phát mọi điểm.

### Ví dụ — Admin Radar

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/boat-tracking", { accessTokenFactory: () => getToken() })
  .withAutomaticReconnect()
  .build();

connection.on("boatPosition", (p) => moveMarker(p.boatId, p.lat, p.lng, p.observedAt));
connection.on("weatherAlert", (a) => showAlert(a));

async function sync() {
  await connection.invoke("JoinFleet");
  const boats = await fetch("/api/v1/tracking/boats", { headers: auth() }).then((r) => r.json());
  boats.forEach((b) => moveMarker(b.boatId, b.latitude, b.longitude, b.observedAt));
}

connection.onreconnected(sync);
await connection.start();
await sync();
```

Chú ý: REST snapshot dùng `latitude/longitude`, event realtime dùng `lat/lng`.

### Ví dụ — Captain Portal gửi GPS từ trình duyệt

```ts
navigator.geolocation.watchPosition(
  (pos) =>
    connection.invoke(
      "SendPosition",
      boatId,
      pos.coords.latitude,
      pos.coords.longitude,
      pos.coords.speed != null ? pos.coords.speed * 1.943844 : null, // m/s -> hải lý/giờ
      new Date(pos.timestamp).toISOString()
    ),
  console.error,
  { enableHighAccuracy: true, maximumAge: 0 }
);
```

Geolocation chỉ hoạt động trên HTTPS (hoặc `localhost`).

### Giả lập GPS cho demo (BR-TRK-01)

Bật bằng biến môi trường (xem `.env.example`):

- `TRACKING_SIMULATOR_ENABLED=true`: tàu của các Trip `EnRoute`/`Suspended` chạy dọc các bến (theo `TripStopCall`) mỗi `TRACKING_SIMULATOR_INTERVAL_SECONDS`.
- `TRACKING_SIMULATOR_INCLUDE_SCHEDULED=true`: giả lập cả Trip chưa chạy (khi chưa có API chuyển trạng thái Trip). Khi đó `tripId` trong payload là `null`.

Vị trí giả lập đi qua đúng luồng của GPS thật, chỉ khác `Source = "Simulated"` trong lịch sử.

### Cảnh báo thời tiết (Weather Guard)

Job Hangfire `weather-guard` (mặc định mỗi 15 phút) lấy thời tiết tại các bến của tuyến có chuyến sắp/đang chạy. Gió trung bình ≥ ngưỡng → `Medium`; gió giật ≥ ngưỡng hoặc dông → `High`. Mỗi tuyến chỉ có **một** Incident thời tiết đang mở trong khung `AlertWindowHours`; lượt sau chỉ cập nhật và liên kết thêm chuyến mới. `weatherAlert` chỉ được phát khi **tạo mới** hoặc **tăng mức** nghiêm trọng. **Không chuyến nào bị tự đổi trạng thái** (BR-INC-03): `operationDecision` của từng chuyến để `null` chờ Admin.

| API (Admin) | Mục đích |
|---|---|
| `GET /api/v1/incidents?status=Open&incidentType=Weather&pageNumber=&pageSize=` | Danh sách sự cố, có `affectedTripCount`, `pendingDecisionCount` |
| `GET /api/v1/incidents/{id}` | Chi tiết + `affectedTrips[]` (giờ khởi hành, trạng thái chuyến, quyết định) |
| `POST /api/v1/incidents/weather-check` | Chạy Weather Guard ngay (demo), trả `{ routesChecked, incidentsCreated, incidentsUpdated, observationFailures }` |

Demo không cần API key: `WEATHER_PROVIDER=Fake` + `WEATHER_FAKE_WIND_MS=15` (hoặc `WEATHER_FAKE_CONDITION_CODE=211`) rồi gọi `weather-check`.

### Đổi trạng thái chuyến (vận hành)

Vòng đời theo BR-OPS-01..03. `Scheduled → Boarding → EnRoute → Arrived → Completed`; `Suspended` là tạm ngưng có thể tiếp tục (không tự gây hoàn tiền); `Cancelled` chỉ cho chuyến **chưa chạy**; chuyến **đã chạy** phải kết thúc bằng `Terminated`. "Đã chạy" nghĩa là `actualDepartureTime` đã có (không suy từ trạng thái của chuyến đang Suspended).

| Thao tác (`operation`) | Từ trạng thái | Sang | Ai làm |
|---|---|---|---|
| `OpenBoarding` | Scheduled | Boarding | Admin, Captain của tàu |
| `Start` | Scheduled, Boarding | EnRoute (ghi `actualDepartureTime`) | Admin, Captain của tàu |
| `Arrive` | EnRoute | Arrived (ghi `actualArrivalTime`) | Admin, Captain của tàu |
| `Complete` | Arrived | Completed | Admin |
| `Suspend` | Scheduled, Boarding, EnRoute | Suspended | Admin |
| `Resume` | Suspended | EnRoute nếu đã chạy, không thì Scheduled | Admin |
| `Cancel` | Scheduled, Boarding, Suspended (chưa chạy) | Cancelled | Admin |
| `Terminate` | EnRoute, Suspended (đã chạy) | Terminated | Admin |
| `NoAction` | — (chỉ ghi quyết định cho một sự cố, không đổi trạng thái) | — | Admin |

| API | Quyền | Mục đích |
|---|---|---|
| `GET /api/v1/trips/operations?from=&to=&status=` | Admin (mọi tàu), Captain (chỉ tàu của mình) | Chuyến để vận hành, mặc định từ 12 giờ trước tới 7 ngày sau (tối đa 31 ngày, 200 chuyến). Mỗi chuyến kèm `allowedOperations` hiện hợp lệ |
| `POST /api/v1/trips/{tripId}/status` body `{ operation, reason?, incidentId? }` | Admin, Captain (chỉ OpenBoarding/Start/Arrive trên tàu mình) | Đổi trạng thái. `Suspend`/`Cancel`/`Terminate`/`NoAction` bắt buộc có `reason` (≤ 500 ký tự). Có `incidentId` thì ghi quyết định vào `IncidentTrip` của chuyến đó (chỉ Admin) |
| `POST /api/v1/trips/{tripId}/sos` body `{ note? }` | Captain của tàu | Tạo sự cố `Safety`/`High` nguồn `Captain`, liên kết chuyến, phát `incidentRaised`. **Không đổi trạng thái chuyến** (BR-INC-04) |
| `GET /api/v1/trips/operations/events?tripId=&take=` | Admin | Nhật ký thao tác (`TripOperationEvent`), mới nhất trước, kèm tên người thực hiện |

Lỗi: thao tác không hợp lệ ở trạng thái hiện tại hoặc chuyến chưa có tàu → **409** `ProblemDetails`; sai quyền hoặc không phải thuyền trưởng của tàu → **403**; thiếu lý do → **400**; chuyến/sự cố không có → **404**. Hai người cùng đổi một chuyến: người đến sau nhận **409** "vừa được thay đổi bởi người khác" (khoá lạc quan `RowVersion`).

Mỗi lần đổi trạng thái ghi một `TripOperationEvent` và phát `tripStatusChanged` **sau khi đã lưu**. Chuyến `Cancelled`/`Terminated` là điều kiện để luồng hoàn tiền (`/api/v1/refunds`) xử lý; `Suspended` không hoàn tiền.

## 2. `/hubs/seat-sync` — đồng bộ sơ đồ ghế

Cho phép **ẩn danh (Guest)**, **chỉ đọc**. Giữ/nhả ghế luôn qua REST API, hub không có method nghiệp vụ.

| Method | Tác dụng |
|---|---|
| `JoinTrip(tripId)` / `LeaveTrip(tripId)` | Nhận thay đổi ghế của 1 Trip |

| Event | Group | Payload |
|---|---|---|
| `seatHeld` / `seatReleased` / `seatSold` | `trip:{tripId}` | `{ tripId, seatId, boardingVisitOrder, disembarkingVisitOrder, status, expiresAt?, version }` |
| `tripStatusChanged` | `trip:{tripId}` | Như trên. Khi chuyến bị Suspended/Cancelled/Terminated thì trang chọn ghế nên báo khách và tải lại `GET /trips/{id}` (`canBook`) |

Ghế được giữ **theo đoạn** (`VisitOrder` của TripStopCall). Client tự xét: ghế chỉ bận với khách hiện tại nếu đoạn trong event **chồng lấn** đoạn khách đang chọn, tức `boarding < myDisembarking && disembarking > myBoarding`.

> Trạng thái: hub và publisher đã có. Việc **phát** các event này phụ thuộc luồng CheckoutSession/giữ ghế (TV2). Hold hết hạn TTL không tự sinh event, nên client cần gọi lại REST sơ đồ ghế khi reconnect và định kỳ.
