# Phần Thịnh trong P3-BE-02 — geofence audio tại POI

Roadmap repo gộp `Geofence + ETA` thành `P3-BE-02` và đặt dependency `P3-BE-01 BoatTrackingHub`. **Thịnh chỉ làm geofence Boat–POI để kích hoạt audio Sightseeing** theo phân công Drive và xác nhận scope của Thịnh. ETA tới bến và khoảng cách tới Station **không thuộc task này**.

## Mục tiêu của Thịnh

- Nhận tọa độ Boat hợp lệ từ contract tracking của Kiệt/owner `P3-BE-01`.
- Tính khoảng cách từ Boat tới POI theo mét và xác định đã vào `RoutePoi.TriggerRadiusM` chưa.
- Chỉ đề xuất trigger cho Route/Trip Sightseeing phù hợp, POI/RoutePoi active và audio tiếng Việt đã publish.
- Ngăn trigger trùng không mong muốn trong cùng lượt hành trình (`BR-EXP-05`).
- Không đề xuất phát khi Trip Suspended/Terminated hoặc đang có thông báo an toàn (`BR-EXP-06`). Onboard Player vẫn phải thực thi ưu tiên an toàn.

**Ranh giới hoàn thành:** Thịnh làm cả phép tính khoảng cách **và logic quyết định/phát lệnh trigger audio**. Phép tính thuần ở mục tiếp theo là giai đoạn đầu để làm độc lập; chỉ sau khi nối với dữ liệu Boat/Trip/POI và gửi trigger theo contract thì phần geofence được giao mới hoàn tất. Kiệt sở hữu nguồn vị trí/hub; owner Onboard Player sở hữu việc phát MP3 ra loa.

## Làm độc lập ngay: phép tính thuần

1. Tạo hàm khoảng cách địa lý đúng đơn vị mét. Roadmap nêu NetTopologySuite; nếu dùng nó phải xử lý hệ tọa độ đúng. Nếu chọn Haversine cho core, ghi lý do và báo nhóm vì khác công nghệ roadmap.
2. Tạo hàm kiểm tra điểm Boat nằm trong bán kính POI. Latitude/longitude hợp lệ, radius dương; `TriggerRadiusM` hiện nullable, không tự đặt default.
3. Test cùng tọa độ, khoảng cách đã biết, ngay trong/ngoài/đúng biên, tọa độ lỗi và radius null.
4. Không đọc DB, không viết hub/event, không làm ETA trong phần core.

Prompt giao agent ở [IMPLEMENT_NOW.md](IMPLEMENT_NOW.md), lát cắt 1.

## Tích hợp sau với nhóm

- Owner tracking cung cấp payload vị trí gồm BoatId, TripId, tọa độ, thời gian và nguồn; nguồn cũ/không đúng Trip phải bị loại.
- Owner POI cung cấp RoutePoi/POI và published AudioGuide `vi` theo Route/Trip thực tế.
- Chốt cách xử lý `TriggerRadiusM = null`, chống phát lặp cho tuyến vòng/nhiều lượt ghé và contract lệnh cho Onboard Player.
- Event/payload `TriggerPOI` trong Drive chỉ là ví dụ. Không tự thêm hub, endpoint hoặc thay contract của Kiệt.
- Backend chỉ phát lệnh/đề xuất; thiết bị trên Boat mới xác nhận đã phát ra loa (`BR-EXP-03`).

Chi tiết và test end-to-end ở [INTEGRATION_LATER.md](INTEGRATION_LATER.md).

## Không làm trong task này

- ETA, tốc độ tới bến, khoảng cách Boat–Station, Arrival Countdown HUD.
- BoatTrackingHub, GPS ingestion, AIS fallback hoặc Radar Fleet.
- CRUD POI, Azure TTS adapter, UI Captain/Passenger, Onboard Player implementation.

## Nghiệm thu phần geofence của Thịnh

1. Core khoảng cách Boat–POI có unit tests và tolerance rõ ràng.
2. Khi tích hợp với Trip Sightseeing thật/mô phỏng, đi vào POI hợp lệ tạo đúng một trigger cho lượt đó; GPS gửi lặp không phát lại tùy tiện.
3. Trip Regular, Suspended/Terminated, audio chưa publish và cảnh báo an toàn không tạo lệnh phát tham quan.
4. Build/test pass, diff không chứa ETA/Station-distance/BoatTrackingHub.
