# Smart Waterbus — rà soát Conceptual & Logical ERD sau BR v2.0 FINAL

**Cơ sở:** `Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29` và hai file Mermaid/DBML được tạo trước khi người dùng yêu cầu kiểm tra. **Phạm vi:** kiểm tra domain semantics, thuộc tính, khóa, quan hệ và chạy mô phỏng các tình huống nghiệp vụ trên SQLite trong bộ test này. **Không phải** kết quả chạy ứng dụng .NET thật, cổng thanh toán thật, thiết bị scanner thật hoặc dịch vụ loa thật.

## 1. Kết luận ngay

- **Conceptual ERD cũ:** Không nhất thiết “sai” theo mọi phương pháp vẽ ERD, nhưng không phù hợp với phong cách **Conceptual chỉ entity + relationship + cardinality** mà nhóm đang cần. Có PK, UUID, kiểu dữ liệu và các cột chi tiết; không phân biệt ranh giới các tầng. Bản `01_Conceptual_ERD_NO_ATTRIBUTES.mmd` đã bỏ hoàn toàn các danh sách thuộc tính.
- **Logical ERD cũ:** Là **starter chưa hoàn chỉnh**, không nên coi là schema sẵn sàng tạo migration. Thiếu cấu trúc xác thực, secure guest access, lịch dừng ở cấp Schedule, thông tin thiết bị offline, phân bổ refund theo Ticket, một phần audit/tracking và các timestamp cần cho luồng thực tế. Bản `02_Logical_ERD_REVIEWED.dbml` bổ sung các trường/quan hệ đó.
- **Trạng thái:** BR FINAL không thay đổi. Logical ERD là **đề xuất kỹ thuật chi tiết** để nhóm rà soát trong bước Logical Design, không phải quyết định bắt buộc phải xây toàn bộ 33 bảng ngay. Việc thêm AuthAccount, AccessGrant, ScannerAssignment, TripOperationEvent, BoatPositionEvent… là lựa chọn triển khai giải quyết yêu cầu BR, không phải BR mới.

## 2. Phân biệt cấp độ ERD

| | Conceptual | Logical | Physical |
|---|---|---|---|
| Mục tiêu | Những thực thể nghiệp vụ nào có quan hệ gì? | Dữ liệu nào cần lưu để thực hiện nghiệp vụ? | Cài đặt cụ thể trên PostgreSQL/SQL Server… |
| Thuộc tính | Có thể hiển thị thuộc tính nếu dùng một quy ước khác; trong **bản này cố ý không hiển thị** | Có bảng/cột, PK, FK, nullable, unique và vài chỉ dẫn cấu trúc | DDL chính xác, chỉ mục thực tế, constraint, transaction, concurrency |
| Guest | Actor mua vé không cần account; minh hoạ bằng đường đứt | **Không có bảng Guest**; `PurchaseOrder.passenger_account_id` nullable | Kiểm tra token và authorization trong API |
| Roles | Passenger, Guest, Admin, Staff, Captain hiển thị riêng | Passenger/Admin/Staff/Captain là bảng profile riêng và liên kết `AuthAccount`; Guest không lưu account | Auth flows, permissions, providers |

**Quan trọng:** Nếu thầy chỉ chấp nhận entity lưu trong DB trên Conceptual ERD, `GUEST` có thể chuyển sang Use Case/Actor diagram; không biến nó thành bảng Guest. Bản Mermaid giữ nhãn GUEST để thể hiện 5 actor đã được yêu cầu và ghi chú rõ sự khác biệt.

## 3. Các lỗi/thiếu sót cụ thể đã xử lý

| Khu vực | Vấn đề file cũ | Phương án trong bản đề xuất sửa |
|---|---|---|
| Concepts | `uuid` / `PK` / `datetime` ở Conceptual | Mermaid entity-only, không field. Có thêm 3 sơ đồ con để dễ nhìn khi draw.io dàn trải. |
| Login / vai trò | Admin/Staff/Captain gần như chỉ có ID+name; không có xác thực | `AuthAccount` (email, provider, subject/password hash, status, thời điểm) + profile `Passenger`, `Admin`, `Staff`, `Captain` riêng với tên/code/active khi thích hợp. Không có bảng Guest. |
| Guest link | Thiếu token hash, expiry, revocation và scope | `AccessGrant` với ManageOrder/PassengerTrip, token hash, expiration/revocation, Order/Booking scope. |
| Schedule và bến | Schedule không có mẫu lịch phục vụ từng bến | `ScheduleStop` (stop + visit order + offset); tạo `TripStopCall` (planned/actual, check-in times) theo từng Trip. Hỗ trợ short-run, chiều ngược và ad-hoc. |
| Trip/Boat | Đổi tàu và captain, tránh trùng lịch | Trip có boat_id optional lúc nháp, bắt buộc trước mở bán; Captain cố định 1:1 với Boat; remap SeatReservation theo SeatCode + SeatClass khi đổi tàu có vé. |
| Giá vé | Thiếu hiệu lực thay đổi bảng giá / historical snapshot | `FareRule` chỉ theo TripType × SeatClass, `effective_from/to` **chỉ phục vụ quản lý phiên bản bảng giá**, KHÔNG phải TimeBand/giá động theo ngày. Ticket lưu face/paid snapshots. |
| Order/Booking | QR Booking thiếu public id và issue time | Booking có public ID, version, issued time; Ticket chỉ sinh sau xác minh thanh toán. |
| Refund nhóm | Không biết cụ thể Ticket được hoàn bao nhiêu | `FinancialTransaction` chung Payment/Refund + `RefundTicketAllocation` cho Ticket đủ điều kiện. Đây **không phải** bảng Refund độc lập. |
| Refund thủ công | Thiếu người xác nhận và chứng từ | `manual_processed_by_admin_id`, `manual_refund_evidence`, trạng thái và thời điểm xử lý. |
| Offline scanner | Thiếu scanner được phân công và bến quét | `ScannerDevice`, `ScannerAssignment`, `CheckInEvent` có call id, device, client event id, event time/server time, sync status. |
| Incident | Một alert thời tiết không nên sinh trùng vô số Incident | Incident có scope; IncidentTrip ghi nhận Trip bị ảnh hưởng, TripOperationEvent ghi audit quyết định. |
| GPS / Sightseeing | Không có persisted positions | `BoatPositionEvent` là đề xuất lưu lịch sử (hoặc thay bằng event stream); POI/AudioGuide tiếng Việt phát tại thiết bị trên tàu. |

## 4. Những trường hợp đã mô phỏng và chạy PASS

Tập tin `03_Model_Flow_Tests.py` đọc DBML được viết ra, dựng schema có **33 bảng và 63 FK**, bật FK và tạo SQLite in-memory. Sau đó thực thi các luồng sau:

1. Một ghế Regular A→B và B→D trên cùng Trip được đặt hai lần khi không chồng lấn; A→C giao B→D bị từ chối; giá cùng SeatClass không lệ thuộc đoạn đi.
2. Từ Schedule xác định được Trip, Boat, Captain qua JOIN; Admin tạo Trip ad-hoc với Schedule NULL.
3. Guest mua vé nhóm; chưa trả tiền thì không có Ticket; xác nhận thanh toán mới phát Ticket; idempotency không tạo payment trùng.
4. Guest manage-order và passenger-link dưới dạng hash token; DB không có bảng Guest, chặn token hash trùng.
5. Staff check-in tại bến B khi Trip EnRoute; chặn quét tại sai bến, sai scanner primary và duplicate event.
6. Đổi Boat có ghế cùng mã/hạng giữ được đặt chỗ; đổi sang tàu có hạng ghế khác bị từ chối.
7. Trip Terminated: Booking đã đến đúng bến không refund, đoạn bị dừng refund 100%; NoShow không refund; thủ công giữ Processing tới khi có chứng từ, chặn hoàn lặp.
8. Round-trip chung một payment: lượt về bị Cancelled chỉ refund phần lượt về.
9. Incident thời tiết trên Route liên kết nhiều Trip, Admin xử lý từng Trip; không tự đổi trạng thái mọi Trip.
10. Sightseeing riêng có bến lên và xuống là cùng Station vật lý ở hai lần ghé; POI audio vi; Suspended thì playback phải tạm dừng theo nghiệp vụ.
11. Cùng **một Booking nhóm** có người CheckedIn và NoShow: chỉ người đủ điều kiện nhận refund; giá vé cũ không thay đổi khi sửa FareRule hiện tại.
12. Không bán nếu chưa gán Boat hoặc quá SalesCloseAt; không kết luận NoShow khi còn offline event chưa đồng bộ.
13. Truy vấn bến lên/xuống phải thuộc cùng Trip/đúng thứ tự, FK chặn refund link không tồn tại, UNIQUE chặn Captain gắn hai Boat.

**Cần hiểu đúng chữ PASS:** Đây là **mô phỏng nghiệp vụ trên schema đề xuất**, không phải kiểm thử toàn bộ application. Tập test chủ động có các hàm validate quy tắc cross-table; không có nghĩa chỉ tạo PK/FK như DBML là tất cả rule tự động được DB enforce. Chưa kiểm thử gateway mạng thật, EF Core migrations, race condition thật, SQLite mobile và thiết bị phát âm thanh thực tế.

## 5. Những bất biến phải enforce trong .NET/service hoặc database bổ sung

1. `Trip.RouteId` phù hợp `Trip.Schedule.RouteId` (nếu có); `Trip.TripType` phù hợp `Route.service_type`; ScheduleStop và TripStopCall phải dùng RouteStop cùng Route, đúng visit order.
2. Trip thuộc Schedule hay ad-hoc **đều phải có ít nhất hai lần dừng/phục vụ phù hợp** trước khi mở bán; bến lên/xuống Booking phải là **hai TripStopCall thuộc chính Trip đó**, visit_order lên nhỏ hơn xuống. Tuyến Sightseeing vòng cho phép Station trùng nhưng visit_order khác.
3. ScheduleStop có thời điểm offset hợp lệ; sinh TripStopCall lịch tương lai; không cập nhật retroactive các Trip đã bán.
4. Trước mở bán, Boat được phân công, có ghế tương thích, chưa bị gán vào Trip khác với thời gian vận hành chồng lấn. Mỗi Boat có một Captain; uniqueness trên Boat.captain_account_id không đủ để đảm bảo mọi Captain *luôn* có Boat trước khi phân công — enforce theo nghiệp vụ.
5. Đúng một giá **có hiệu lực** cho mỗi `(TripType, SeatClass)` tại thời điểm mua; các dải hiệu lực FareRule không được chồng lấn. `effective_from` phản ánh lần Admin cập nhật bảng giá, không thay đổi giá theo giờ chạy.
6. Sau Checkout Review, Order có một hoặc hai Booking theo OneWay/RoundTrip. Ticket chỉ phát hành một lần từ SeatReservation khi IPN/backend xác nhận payment thành công; giữ giá tại lúc checkout/payment theo quy trình đã chọn.
7. Không bán/giữ ghế sau SalesCloseAt hoặc Trip departure; không được giữ/cấp ghế trùng **Trip + Seat + segment** cho các reservation còn hiệu lực hoặc Redis holds. Phần chặn cạnh tranh phải được kiểm tra transactionally tại backend/DB, không chỉ kiểm tra ở UI.
8. SeatReservation.Seat thuộc Boat của Booking.Trip; khi đổi Boat sau bán, tất cả ghế đã bán phải được remap sang mã ghế + class tương thích một cách nguyên tử. Không thay đổi lịch sử fare snapshot.
9. `Booking.paid_allocation = sum(Ticket.paid_fare_snapshot)` theo Booking đã thanh toán (với cách phân bổ đã thống nhất); refund `FinancialTransaction.amount = sum(RefundTicketAllocation.amount)`; không tạo refund cho Ticket NoShow và không hoàn vượt số đã thanh toán hoặc hoàn trùng Ticket.
10. Với FinancialTransaction: Payment không có refund fields; Refund bắt buộc có Booking và original Payment thành công **thuộc cùng Order**. Refund thủ công chỉ Succeeded sau khi có bằng chứng và người xác nhận.
11. Mỗi bến/TripStopCall chỉ một primary offline writer còn hiệu lực. Staff/check-in phải đúng boarding call, window, Ticket, Trip và quyền tác nghiệp. Offline sync idempotent; xem xét event đến muộn trước khi chốt NoShow.
12. `AccessGrant.booking_id` nếu có phải thuộc `AccessGrant.order_id`; `ManageOrder` không cần booking, `PassengerTrip` cần Booking; token chỉ lưu dạng hash, kiểm tra scope/expiry/revocation, không auto-link account khi chỉ trùng email.
13. Incident có scope phù hợp và danh sách Trip bị ảnh hưởng; Incident không tự thay đổi TripStatus. Admin/Captain chỉ làm thao tác được phân quyền, mọi chuyển trạng thái quan trọng có audit.
14. Sightseeing Route khác Regular, không cho vé khác Trip lên tàu; POI audio chỉ bản `vi` được phát qua onboard player khi Trip phù hợp, nhường quyền cho cảnh báo an toàn. Không bắt buộc offline audio theo BR.
15. Không cho phép client gửi dữ liệu tài chính/thông tin check-in không được xác thực rồi ghi thẳng vào hệ thống. Bảo vệ PII, secret và tiền của khách (encryption/authorization/backup) trong triển khai.

**Những ràng buộc trên không tự xuất hiện chỉ vì đã có FK/UNIQUE.** Nếu áp PostgreSQL, một phần cần exclusion/partial unique constraints, checks, DB transactions; những phần phụ thuộc nghiệp vụ cần validate ở backend. Không dùng đơn thuần một unique `(trip_id, seat_id)` vì sẽ phá seat reuse theo segment.

## 6. Về số entity và thuộc tính của Role

- Không có quy định Logical ERD mỗi bảng phải có nhiều thuộc tính. Một bảng subtype như `Staff` có thể chỉ chứa `account_id`, `employee_code`, `full_name`, cờ active… vì email, provider và trạng thái đăng nhập nằm trong `AuthAccount`. Đây là **chuẩn hoá có chủ đích**, không phải thiếu thiết kế.
- Thầy yêu cầu Passenger, Guest, Admin, Staff, Captain tách role ở mặt nghiệp vụ. Conceptual thể hiện riêng; Logical có thể dùng `AuthAccount` **chỉ cho phần xác thực**, không nhập năm vai trò lại một bảng User duy nhất. `Guest` không cần record vì là người mua không đăng nhập.
- **Không tự thêm tuổi, địa chỉ, số CCCD, giấy phép, lương…** khi BR không đòi hỏi; thêm dữ liệu nhạy cảm không cần thiết sẽ tăng rủi ro và làm mô hình nặng.
- Logical lớn hơn Conceptual vì có các bảng triển khai: AuthAccount, AccessGrant, ScannerDevice, ScannerAssignment, TripOperationEvent, BoatPositionEvent… Nhóm có thể triển khai từng giai đoạn nhưng nếu bỏ bảng thì phải có cơ chế thay thế đáp ứng BR tương đương.

## 7. Vấn đề còn cần kiểm tra bằng công cụ thật trước khi coi Logical là triển khai được

- Thử **import Mermaid** vào draw.io (tập lệnh ER này đã được kiểm tra cấu trúc tuyến tính và không có attribute nhưng **chưa qua Mermaid renderer chính thức trong môi trường này**).
- Thử **import DBML** vào dbdiagram.io (đã phân tích các bảng, cột, ref và sinh chạy SQLite schema, **chưa chạy parser DBML chính thức trong môi trường này**).
- Phải duyệt FK/check constraints, unique/exclusion, index, timezone, transaction isolation và decimal precision trong database mục tiêu (PostgreSQL/SQL Server), kèm test concurrency thật và mô phỏng callback thanh toán đến muộn.
- BR FINAL có thể là chuẩn để vẽ; **không có nghĩa toàn bộ các bảng hỗ trợ trong Logical bắt buộc phải được build cùng lúc trong môn học**.
