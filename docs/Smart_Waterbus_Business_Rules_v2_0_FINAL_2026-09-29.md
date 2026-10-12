# SMART WATERBUS

## BUSINESS RULES SPECIFICATION — FINAL

**Phiên bản:** v2.0 — 29/09/2026  
**Ngôn ngữ:** Tiếng Việt  
**Trạng thái:** BẢN FINAL — các quyết định nghiệp vụ đã chốt; thông số vận hành được cấu hình theo chính sách hệ thống.  
**Mục đích:** Làm nguồn chuẩn để nhóm chỉnh Conceptual ERD, sau đó triển khai Logical ERD, API, UI và kiểm thử.

> **Nguyên tắc đọc:** Tài liệu này thay thế định hướng nghiệp vụ trong bản *Smart Waterbus — Business Rules Proposed v1.0* ngày 22/09/2026 ở những nội dung có mâu thuẫn. Đây là mô tả nghiệp vụ của nhóm, không phải khẳng định về cách vận hành nội bộ của đơn vị Waterbus ngoài thực tế. Ví dụ thời gian, mã ghế và số tiền chỉ nhằm minh họa, không phải tham số kinh doanh đã chốt.

### Quy ước trạng thái

| Ký hiệu | Ý nghĩa |
|---|---|
| **[CHỐT]** | Nhóm đã xác nhận định hướng nghiệp vụ; áp dụng cho bản BR hiện tại. |
| **[CẤU HÌNH]** | Chính sách đã rõ về mặt logic, nhưng giá trị/giới hạn cụ thể chưa cần hard-code và sẽ cấu hình sau. |
| **[NGOÀI PHẠM VI]** | Chủ động không triển khai trong phạm vi môn học hiện tại. |

## 1. Phạm vi sản phẩm và vai trò

**BR-SCOPE-01 [CHỐT]** Smart Waterbus cung cấp đặt vé tàu đường sông, lựa chọn chỗ ngồi, thanh toán, nhận vé/QR, quản lý chuyến, theo dõi hành trình, check-in và chế độ tham quan Sightseeing.

**BR-SCOPE-02 [CHỐT]** Passenger sử dụng **Web responsive** (prototype thiết kế desktop-first). Captain và Admin sử dụng Web. **Staff Scanner** là luồng dành riêng cho mobile. Không có ứng dụng Passenger Mobile hoặc Captain Mobile độc lập trong scope môn học.

**BR-ROLE-01 [CHỐT]** Hệ thống xác định **năm actor/role nghiệp vụ**: **Passenger, Guest, Admin, Staff, Captain**. **Không có Dispatcher**; Admin đảm nhận phần điều phối vận hành trước đây từng dự kiến cho Dispatcher.

**BR-ROLE-02 [CHỐT]** **Passenger** là người sử dụng các tính năng hành khách, có thể đăng nhập nếu sở hữu tài khoản. Danh tính hành khách trên Ticket không bắt buộc tương ứng với một account.

**BR-ROLE-03 [CHỐT]** **Guest** sử dụng chức năng tra cứu, mua vé, thanh toán và truy cập giao dịch được cấp phép **không cần đăng ký/đăng nhập**. Guest là actor nghiệp vụ, không buộc phải tạo một bản ghi tài khoản Guest trong DB.

**BR-ROLE-04 [CHỐT]** **Admin** cấu hình dữ liệu hệ thống và quyết định các thao tác vận hành được phân quyền như Cancel, Suspend, Resume, Terminate, xử lý Incident và những thay đổi Trip liên quan.

**BR-ROLE-05 [CHỐT]** **Staff** xác minh QR và check-in từng Ticket tại đúng bến, bao gồm chế độ online và offline được phân công.

**BR-ROLE-06 [CHỐT]** **Captain** phụ trách an toàn tàu, cập nhật thông tin vận hành, báo cáo Incident và thực hiện các thao tác Start/Arrive thuộc quyền sau khi backend xác thực điều kiện. Captain không tự ý thay thế quyền phê duyệt trạng thái Cancel/Terminate của Admin trong hệ thống.

**BR-ROLE-07 [CHỐT]** Trên Conceptual ERD, nhóm có thể biểu diễn năm role nghiệp vụ tách biệt như thầy yêu cầu. Khi thiết kế xác thực ở mức Logical/Physical, được dùng chung hạ tầng danh tính và phân quyền cho các actor có account; tránh đồng nhất **Guest**, **Purchaser**, **Passenger trên Ticket** với một tài khoản bắt buộc.

## 2. Route, Station và hai loại dịch vụ

**BR-ROUTE-01 [CHỐT]** `Route` là một **tuyến dịch vụ có lộ trình/bến được xác định**, không chỉ là tên con sông. `Station` là bến vật lý; cùng một Station có thể xuất hiện trên nhiều Route.

**BR-ROUTE-02 [CHỐT]** `RouteStop` là vị trí xuất hiện của Station trong Route, có thứ tự/sequence rõ ràng. Các điểm lên/xuống phải được đối chiếu theo thứ tự di chuyển thực tế của Trip; không giả định mọi chuyến đều chạy theo sequence tăng.

**BR-ROUTE-03 [CHỐT]** **Regular/Commuter** và **Sightseeing** có **Route dịch vụ riêng**, dù có thể đi qua một số đoạn sông hoặc địa điểm giống nhau. Loại dịch vụ là thuộc tính của Schedule/Trip, không phải thuộc tính của hình thức mua one-way/round-trip.

**BR-ROUTE-04 [CHỐT]** Regular cho phép khách lên/xuống tại những bến hợp lệ trên một Trip, kể cả bến trung gian. Booking chỉ chọn cặp boarding–disembarking thuộc phần lộ trình mà Trip phục vụ và đúng hướng đi.

**BR-ROUTE-05 [CHỐT]** Sightseeing sử dụng hành trình tham quan riêng; không cho hành khách tự chọn tùy ý đoạn lên/xuống như Regular. Điểm đón/trả và hành trình tham quan phải được cấu hình ở cấp dịch vụ/Trip.

**BR-ROUTE-06 [CHỐT]** Nếu hành trình Sightseeing trở về chính bến xuất phát, hai sự kiện **lên** và **xuống** vẫn khác nhau về vị trí thời gian/thứ tự tuyến, ngay cả khi cùng một Station vật lý. Tránh áp dụng máy móc quy tắc `FromStationId != ToStationId` của Regular cho chuyến vòng.

**BR-ROUTE-07 [CHỐT]** Hỗ trợ Trip Regular chạy một phần Route (short-run) theo Schedule: Schedule xác định hướng và các bến thực sự phục vụ, bao gồm bến đầu/cuối của đoạn chạy. Trip chỉ được bán những cặp boarding–disembarking thuộc các bến phục vụ theo đúng thứ tự hành trình. Trip ad-hoc cũng phải khai báo rõ phạm vi bến phục vụ.

## 3. Schedule, Trip, Boat và Captain

**BR-SCH-01 [CHỐT]** Schedule là lịch chạy lặp lại/mẫu, gồm Route, hướng/đoạn hành trình, giờ khởi hành, ngày hoạt động và khoảng thời gian hiệu lực. Trip là chuyến cụ thể theo ngày giờ: có thể được sinh từ Schedule hoặc do Admin tạo riêng ngoài Schedule (ad-hoc).

**BR-SCH-02 [CHỐT]** Giữ chuỗi nghiệp vụ Route → Schedule → Trip → Boat ↔ Captain cho Trip sinh từ lịch. Với Trip ad-hoc, liên kết trực tiếp Route → Trip và không yêu cầu Schedule. Không ràng buộc Boat cố định vào Schedule, cũng không cần lưu Captain trực tiếp vào Schedule.

**BR-SCH-03 [CHỐT]** Một Schedule có thể sinh nhiều Trip theo các ngày; mỗi Trip có 0..1 Schedule nguồn. Mỗi Trip bắt buộc thuộc đúng một Route và vận hành một đoạn/hành trình cụ thể; nếu có Schedule thì Route và đoạn phục vụ của Trip phải nhất quán với Schedule. Trip có Boat được phân công trước khi mở bán; Captain được xác định qua Boat.

**BR-SCH-04 [CHỐT]** **Boat phải được phân công trước khi mở bán ghế của Trip**, vì inventory và loại ghế xuất phát từ Boat. Không cho phép xác nhận giao dịch chọn ghế khi Trip chưa có Boat/seat inventory hợp lệ.

**BR-SCH-05 [CHỐT]** Captain gắn với Boat **1:1 cố định trong phạm vi hiện tại**; không thiết kế Captain thay ca/dự phòng ở phiên bản môn học. Một Boat có một Captain; một Captain phụ trách một Boat.

**BR-SCH-06 [CHỐT]** Thay đổi Schedule không tự động viết lại các Trip đã có giao dịch/Booking được xác nhận. Hủy hoặc thay đổi một chuyến cụ thể tác động đến Trip đó, không mặc nhiên sửa cả Schedule lặp lại.

**BR-SCH-07 [CHỐT]** Khi đổi Boat cho Trip **đã bán ghế**, Boat thay thế phải có mã ghế và SeatClass tương thích với tất cả SeatReservation/Ticket đã bán. Backend remap SeatReservation sang Seat của Boat thay thế một cách nhất quán; không tự hạ hạng, cấp ghế khác hoặc âm thầm thay đổi quyền lợi.

**BR-SCH-08 [CHỐT]** Không được gán cùng một Boat vào các Trip có thời gian vận hành chồng lấn. Khi phân công cần tính thời gian chạy/dừng hợp lý; không chỉ đối chiếu một giờ khởi hành.

**BR-SCH-09 [CHỐT]** Có thể truy vấn các Trip/Boat/Captain của một ngày thông qua quan hệ dữ liệu bình thường. Việc sử dụng JOIN không phải là lý do nghiệp vụ để đảo quan hệ hoặc nhân bản khóa ngoại lên Schedule.

**BR-SCH-10 [CHỐT]** Admin được tạo Trip đặc biệt ngoài Schedule (ad-hoc). Trip này có ScheduleId tùy chọn/để trống nhưng bắt buộc khai báo Route, loại dịch vụ, hướng/đoạn và các bến thực sự phục vụ, ngày giờ vận hành và Boat trước khi mở bán. Trip ad-hoc vẫn tuân thủ kiểm tra trùng lịch Boat, bán ghế, check-in và giá vé như Trip bình thường.

**BR-STOP-01 [CHỐT]** Đối với Regular nhiều bến, mỗi Trip phải có dữ liệu hoặc phép suy diễn đáng tin cậy về **các bến thực sự dừng và thời điểm đến/rời dự kiến** để xử lý boarding theo bến, NoShow và đoạn hành trình đã hoàn tất.

**BR-STOP-02 [CHỐT]** Khi cần ghi thời gian thực tế đến/rời bến, có thể dùng `TripStopCall`/`TripStop` tại Logical ERD. Không được suy việc hoàn tất đoạn đi chỉ từ `TripStatus` toàn chuyến.

## 4. Boat, Seat, SeatClass và giá vé

**BR-SEAT-01 [CHỐT]** Boat sở hữu các `Seat` cụ thể (mã ghế độc lập trong từng Boat). `Seat` thuộc một `SeatClass` như Standard/Premium; tên các hạng và mức giá do Admin cấu hình.

**BR-SEAT-02 [CHỐT]** **Không dùng `SeatLayout` entity** trong scope hiện tại. Không yêu cầu giữ `TripSeat` như entity bắt buộc; cách biểu diễn ưu tiên là **Boat → Seat**, **Booking → SeatReservation → Seat**, và **Booking → Trip**.

**BR-SEAT-03 [CHỐT]** Mỗi SeatReservation biểu diễn một ghế được chọn cho một hành khách trong một Booking/đoạn đi. `SeatReservation` chứa passenger snapshot phục vụ xuất Ticket và cho phép có trước khi Ticket được phát hành.

**BR-SEAT-04 [CHỐT]** **Cho phép dùng lại cùng Seat trên cùng Trip cho các đoạn không chồng lấn**: ví dụ A→B và B→D có thể cùng Seat A1; A→C và B→D không thể cùng Seat A1.

**BR-SEAT-05 [CHỐT]** Kiểm tra overlap theo thứ tự di chuyển thực tế của Trip, cho cả ghế đã bán lẫn ghế đang được giữ. Availability không thể dùng một cờ `Sold = true` cho toàn Trip.

**BR-SEAT-06 [CHỐT]** Seat được chọn phải thuộc Boat đang phân công cho Trip, có class và điều kiện sử dụng phù hợp. Việc đổi Boat sau bán phải bảo toàn SeatCode + SeatClass như BR-SCH-07.

**BR-FARE-01 [CHỐT]** Giá vé chỉ phân biệt theo **TripType (Regular/Sightseeing) và SeatClass**. Admin cấu hình mức giá cho từng tổ hợp; không thay đổi theo Route, TimeBand, ngày/giờ, bến lên/xuống hay khoảng cách/đoạn đi.

**BR-FARE-02 [CHỐT]** Đối với Regular, các hành khách cùng **SeatClass** có đơn giá như nhau dù đặt A→B hay A→C, trên bất kỳ Route Regular nào; bến lên/xuống chỉ dùng để quản lý hành trình, ghế và check-in.

**BR-FARE-03 [CHỐT]** Mỗi Ticket lưu **fare snapshot** tại thời điểm thanh toán (bao gồm TripType, SeatClass và số tiền được phân bổ), để việc Admin đổi bảng giá sau này không làm thay đổi lịch sử.

**BR-FARE-04 [CHỐT]** Không có phân loại giá theo tuổi/ngày sinh hoặc nhóm trẻ em/người cao tuổi/sinh viên trong scope hiện tại. Không có surge pricing tự động theo thời gian thực.

## 5. Order, Booking, Passenger và Ticket

**BR-COM-01 [CHỐT]** Mô hình thương mại: **Order → Booking(s) → SeatReservation(s) → Ticket(s)**. Một Order biểu diễn một lần checkout/thanh toán; Booking biểu diễn đúng **một Trip và một đoạn hành trình**; Ticket là quyền sử dụng của một hành khách trên một ghế.

**BR-COM-02 [CHỐT]** Một Order có một Booking nếu mua one-way hoặc hai Booking nếu checkout round-trip. Round-trip là kiểu mua vé; **không phải TripType** và không tạo một Booking gộp cho hai chuyến.

**BR-COM-03 [CHỐT]** Lượt về mặc định có thể là B→A khi lượt đi A→B, nhưng người dùng được chọn **bến bắt đầu chiều về khác bến kết thúc chiều đi**, ví dụ A→B và C→A, nếu các Trip/Route thực sự phục vụ các đoạn đó. Hai Booking có thể khác ngày, giờ, ghế và loại dịch vụ nếu phù hợp điều kiện bán.

**BR-COM-04 [CHỐT]** Mỗi Booking thuộc **đúng một Trip**; tất cả SeatReservation trong cùng Booking chia sẻ một cặp boarding/disembarking. Một Trip có thể có nhiều Booking.

**BR-COM-05 [CHỐT]** Một Booking được mua cho **nhiều hành khách**. Purchaser có thể mua cho người khác, không bắt buộc là người lên tàu.

**BR-COM-06 [CHỐT]** Mỗi SeatReservation có tên Passenger bắt buộc; email và số điện thoại hành khách là tùy chọn. Không lưu DOB/PassengerCategory như dữ liệu bắt buộc trong luồng đặt vé.

**BR-COM-07 [CHỐT]** Order lưu Purchaser FullName và Email bắt buộc, Phone tùy chọn, chụp snapshot khi checkout; không phụ thuộc các thay đổi profile sau này.

**BR-COM-08 [CHỐT]** Checkout Review có thể tạo Order/Booking/SeatReservation nháp, nhưng **chỉ phát hành Ticket sau khi backend xác minh thanh toán thành công**. Một SeatReservation đã thanh toán tương ứng tối đa một Ticket hợp lệ được phát hành.

**BR-COM-09 [CHỐT]** Ticket được check-in theo từng người/ghế, độc lập với những Ticket còn lại trong Booking. Một Booking có thể có Ticket `CheckedIn` và Ticket `NotCheckedIn/NoShow` cùng lúc.

**BR-COM-10 [CHỐT]** Việc Booking thuộc Trip nào phải là relationship trực tiếp trong domain. Không chỉ suy từ SeatReservation → Seat → Boat hoặc từ dữ liệu QR.

## 6. Search, seat hold, checkout và Payment/Refund transactions

**BR-SRCH-01 [CHỐT]** Regular search theo bến lên, bến xuống, ngày đi và tùy chọn one-way/round-trip; PassengerCount là tiện ích tùy chọn. Số hành khách thực tế = số ghế đã chọn.

**BR-SRCH-02 [CHỐT]** Trip chỉ được hiển thị là bán được khi đúng đoạn hành trình, còn ghế hợp lệ, chưa bị hủy và chưa vượt `SalesCloseAt`.

**BR-TIME-01 [CHỐT]** Với scope hiện tại, **đóng bán vé trước khi Trip rời bến xuất phát đầu tiên**. Không mở bán ghế mới sau khi Trip đã bắt đầu hành trình, dù sau đó vẫn tiếp tục check-in tại các bến giữa tuyến.

**BR-TIME-02 [CẤU HÌNH]** Thời điểm đóng bán cụ thể (`SalesCloseAt`) và thời lượng giới hạn do Admin cấu hình; `SalesCloseAt` phải không muộn hơn thời điểm Trip bắt đầu thực tế, và phải đóng trước khi khởi hành theo quy trình vận hành thông thường.

**BR-HOLD-01 [CHỐT]** Khi chọn ghế thành công, hệ thống giữ ghế tạm thời theo đoạn hành trình trong `CheckoutSession` (không khóa theo UserId, để Guest cũng dùng được).

**BR-HOLD-02 [CHỐT]** Một CheckoutSession dùng chung cho toàn Order, gồm cả hai Booking của round-trip. Các hold có hạn chung tính từ lần giữ hợp lệ đầu tiên; chọn/bỏ thêm ghế không tự kéo dài vô hạn.

**BR-HOLD-03 [CHỐT]** Bỏ chọn ghế sẽ giải phóng hold; client mất kết nối/đóng trình duyệt sẽ hết hạn qua TTL. Khi đã gửi yêu cầu thanh toán, có một cửa sổ PaymentPending riêng để chờ gateway xác nhận.

**BR-HOLD-04 [CHỐT]** Redis hoặc cơ chế cache tương đương hỗ trợ temporary holds, nhưng database/backend vẫn phải bảo vệ tính nhất quán khi commit: không cho hai SeatReservation hoạt động trùng ghế + Trip + đoạn hành trình.

**BR-HOLD-05 [CẤU HÌNH]** TTL của checkout hold và PaymentPending được cấu hình; không chốt con số cố định trong BR.

**BR-PAY-01 [CHỐT]** Backend-verified gateway webhook/IPN là bằng chứng xác nhận thanh toán; browser redirect không phải nguồn sự thật cuối cùng. Callback trễ, lặp, sai thứ tự phải được xử lý idempotently.

**BR-PAY-02 [CHỐT]** Payment/Refund có thể dùng **một entity giao dịch chung** (`FinancialTransaction` hoặc `PaymentTransaction` có `TransactionType = Payment/Refund`). Không bắt buộc thêm bảng `Refund` riêng vào Conceptual ERD.

**BR-PAY-03 [CHỐT]** Mỗi lần thử thanh toán là một transaction record có trạng thái/lịch sử riêng. Refund transaction liên kết **original successful payment transaction**, Order và **Booking bị ảnh hưởng**; không tạo refund trùng khi retry hoặc khi gateway timeout.

**BR-PAY-04 [CHỐT]** Một payment thành công có thể thanh toán cho **hai Booking trong một Order**; từng Booking phải có số tiền thực tế được phân bổ rõ ràng, để hoàn tiền được riêng lượt bị ảnh hưởng.

**BR-PAY-05 [CHỐT]** Sau khi payment được xác minh: Booking Confirmed, SeatReservation được xác nhận, Ticket được phát hành, hold tạm được giải phóng. Thanh toán thất bại/hết hạn không phát hành Ticket; callback muộn cần reconciliation trước khi quyết định ghi nhận tiền/ghế.

**BR-PAY-06 [CHỐT]** Tổng tiền hoàn của các refund thành công/đang xử lý trên cùng khoản thanh toán không được vượt quá tiền đã thu có thể hoàn còn lại; số tiền tính theo paid allocation, không chỉ giá niêm yết.

## 7. Guest access và liên kết tài khoản

**BR-ID-01 [CHỐT]** Guest mua vé end-to-end không cần account; `Order.AccountId`/liên kết đến account là tùy chọn. Email Purchaser bắt buộc để xác nhận và liên hệ giao dịch.

**BR-ID-02 [CHỐT]** Guest nhận **Manage Order secure link** với opaque random token có entropy phù hợp; chỉ hash của token được lưu DB. Không tạo token bằng cách hash giá trị đoán được như OrderId + email.

**BR-ID-03 [CHỐT]** Truy cập thông tin tài chính/refund thuộc về Purchaser có quyền, không tự cấp cho mọi người chỉ biết Ticket QR hoặc PassengerName.

**BR-ID-04 [CHỐT]** Người dùng có tài khoản có thể liên kết từng Order mua khi còn là Guest, nhưng cần đăng nhập và chứng minh quyền truy cập Order đó bằng token hợp lệ. **Không tự gán các Order vào account chỉ vì trùng email.**

**BR-ID-05 [CHỐT]** Hành khách không có account/email riêng vẫn có thể xem thông tin đi tàu được cho phép bằng Passenger TripAccess Link do Purchaser chia sẻ. TripAccess không được tiết lộ mặc định chi tiết thanh toán của Purchaser.

**BR-ID-06 [CHỐT]** TripAccess phục vụ theo dõi chuyến/thông tin hành trình; **không dùng để mở khóa audio cá nhân** vì Sightseeing phát audio chung qua loa Boat.

**BR-ID-07 [CẤU HÌNH]** Hạn dùng, rotation và revocation của các secure links do hệ thống cấu hình/chính sách bảo mật quy định, không chốt hằng số trong tài liệu này.

## 8. QR, check-in theo bến và offline Staff Scanner

**BR-QR-01 [CHỐT]** **Mỗi Booking có một QR tĩnh được ký số**, không phải một QR riêng cho mỗi Ticket. One-way có một QR; round-trip (hai Booking) có hai QR.

**BR-QR-02 [CHỐT]** QR chứa identifier công khai tối thiểu, credential version và signature; không chứa dữ liệu hành khách/thanh toán ở dạng plaintext được xem là đáng tin. Verify signature chỉ xác thực mã do hệ thống cấp, **không tự bảo đảm Ticket vẫn có hiệu lực**.

**BR-QR-03 [CHỐT]** Staff scan QR và xem danh sách Ticket của Booking. Staff chọn **đúng những hành khách đang có mặt** để check-in từng Ticket; có thể quét lại QR để check-in các thành viên đến sau trong boarding window.

**BR-BOARD-01 [CHỐT]** Staff check-in tại **khu vực chờ được quản lý của đúng bến lên**, có thể thực hiện **trước khi Boat tới**. Passenger sau đó tự di chuyển vào khu vực chờ được hướng dẫn và lên đúng Boat khi đến.

**BR-BOARD-02 [CHỐT]** Scope đơn giản hóa: **không có lần quét thứ hai**, không thêm trạng thái `Boarded` và không triển khai hệ thống xác nhận vật lý rằng hành khách đã đặt chân lên Boat. `CheckedIn` có nghĩa Staff đã xác minh tại bến, **không phải bằng chứng đã bước lên tàu**.

**BR-BOARD-03 [CHỐT]** Khi Staff quét, backend kiểm tra Booking–Trip, bến lên, cửa sổ check-in của bến, tình trạng Ticket, và quyền tác nghiệp Staff. Staff không được check-in Ticket của Trip khác hoặc bến khác chỉ vì hành khách đứng cùng khu vực.

**BR-BOARD-04 [CHỐT]** Trip Regular có thể đang `EnRoute` nhưng Staff vẫn check-in cho người sẽ lên tại **bến trung gian chưa tới lượt**. Trạng thái `EnRoute` **không tự đóng mọi check-in của toàn Trip**.

**BR-BOARD-05 [CHỐT]** Cửa sổ check-in được xác định **theo bến lên/điểm dừng thực sự được phục vụ**, chứ không chỉ có một `BoardingCloseAt` chung cho bến đầu. `NoShow` chỉ được kết luận khi cơ hội check-in tại **bến lên của Ticket** kết thúc và dữ liệu offline liên quan đã được đối soát hợp lý.

**BR-BOARD-06 [CHỐT]** Khi hai dịch vụ Regular và Sightseeing chia sẻ bến, Staff vẫn kiểm tra **đúng Trip/Boat**. Check-in cho Regular không cấp quyền lên Sightseeing. Việc hướng dẫn xếp hàng/boarding là vận hành tại bến; không bắt buộc thêm hệ thống scan thứ hai.

**BR-OFF-01 [CHỐT]** Scanner được preload manifest dữ liệu Trip/Booking/Ticket liên quan, thông tin bến, public verification key và trạng thái cần thiết vào **SQLite local cache** trước khi tác nghiệp offline; sync sát giờ check-in khi có kết nối.

**BR-OFF-02 [CHỐT]** Khi mất Internet, **chỉ một Primary Offline Scanner được quyền ghi nhận check-in cho cùng phạm vi Trip + boarding point được phân công**. Có thể có scanner khác tại những bến hoặc phạm vi độc lập; không cho nhiều offline writers cùng ghi một tập Ticket/boarding point.

**BR-OFF-03 [CHỐT]** Mỗi check-in offline được lưu thành sự kiện cục bộ có định danh duy nhất, thời gian và thông tin scanner, sau đó **idempotent sync** về backend. Hệ thống đối soát trạng thái và ghi nhận sự kiện duplicate/conflict để review khi cần.

**BR-OFF-04 [CHỐT]** SQLite cache và chữ ký QR không giúp scanner biết các thay đổi phát sinh sau lần preload. Quy trình không xem kết quả offline là bằng chứng tuyệt đối trước khi backend reconciliation, đặc biệt khi xử lý NoShow hoặc Ticket đã thay đổi trạng thái.

**BR-OFF-05 [CHỐT]** `CheckInEvent` lưu lịch sử tác nghiệp; trạng thái check-in hiện hành (`BoardingStatus`) được quản lý trên Ticket hoặc dữ liệu nhất quán tương đương. Một Ticket có thể có nhiều check-in attempts/events nhưng chỉ một kết quả check-in hiệu lực.

## 9. Trip lifecycle, Incident và quyền vận hành

**BR-OPS-01 [CHỐT]** Trip có các trạng thái chính: `Scheduled`, `Boarding`, `EnRoute`, `Suspended`, `Arrived`, `Completed`, `Cancelled`, `Terminated`. `Delayed` là thuộc tính/cảnh báo, không phải TripStatus chính.

**BR-OPS-02 [CHỐT]** `Boarding` ở cấp Trip mô tả giai đoạn trước khi rời bến đầu; check-in theo bến trung gian vẫn có thể diễn ra trong `EnRoute`. Tình trạng cập/rời các bến được theo dõi riêng, không phải lặp `EnRoute → Boarding → EnRoute` ở mỗi bến.

**BR-OPS-03 [CHỐT]** `Cancelled` áp dụng khi Trip không bắt đầu phục vụ. `Terminated` áp dụng khi Trip đã bắt đầu nhưng phải kết thúc sớm. `Suspended` là tạm ngưng có khả năng tiếp tục và **không tự gây refund**.

**BR-OPS-04 [CHỐT]** Captain báo cáo tình huống và có quyền thực hiện hành động bảo đảm an toàn thực tế. Admin quyết định và ghi nhận các chuyển trạng thái vận hành thuộc thẩm quyền hệ thống. Các thao tác Start, Arrive, Resume, Terminate đều phải qua backend validation và audit.

**BR-INC-01 [CHỐT]** Dùng entity **Incident** cho sự cố kỹ thuật, an toàn, thời tiết và sự kiện vận hành có liên quan; **không bắt buộc entity WeatherAlert riêng** trong Conceptual ERD hiện tại. Incident có Source/Type/Severity/Status/Description/Time và người report/handle nếu có.

**BR-INC-02 [CHỐT]** Incident có thể mang **phạm vi một Trip, một Boat hoặc một Route**. Cảnh báo thời tiết của Route không cần nhân bản thành nhiều Incident; có thể liên kết đến nhiều Trip bị ảnh hưởng (ví dụ `IncidentTrip`) khi cần lưu danh sách cụ thể.

**BR-INC-03 [CHỐT]** Phát hiện Incident trên Route **không tự động Cancel/Suspend/Terminate mọi Trip thuộc Route**. Hệ thống xác định các Trip liên quan theo phạm vi và thời gian; Admin quyết định hành động trên từng Trip/nhóm Trip cụ thể và ghi nhận kết quả.

**BR-INC-04 [CHỐT]** Captain/Staff có thể báo cáo Incident được phân quyền; nguồn hệ thống/WeatherService có thể tạo Incident không có `ReportedByUser`. Việc ghi nhận Incident và quyết định thay đổi TripStatus là hai nghiệp vụ khác nhau.

**BR-INC-05 [CHỐT]** Trip bị `Suspended` vẫn tiếp tục cung cấp vị trí khi khả dụng; audio Sightseeing tạm dừng. Nếu tiếp tục, giữ nguyên Ticket/Booking hợp lệ; nếu chấm dứt, chuyển `Terminated` và áp dụng chính sách hoàn tiền theo đoạn dịch vụ.

**BR-TRK-01 [CHỐT]** Theo dõi hành trình sử dụng **GPS/vị trí của Boat**, không phải GPS của Passenger. Có thể sử dụng giả lập vị trí theo Route trong demo, nhưng logic sử dụng vẫn dựa trên vị trí tàu.

## 10. Hoàn tiền khi Cancelled hoặc Terminated

**BR-RF-01 [CHỐT]** Nếu **Trip bị Cancelled trước khi bắt đầu**, hoàn **100% số tiền thực tế đã thanh toán phân bổ cho Booking/Ticket bị ảnh hưởng** qua phương thức thanh toán ban đầu khi gateway hỗ trợ.

**BR-RF-02 [CHỐT]** Nếu **Trip bị Terminated khi đang chạy**, đánh giá từng Booking theo **đoạn hành trình được đặt và tiến độ phục vụ thực tế**, không hoàn tiền mù quáng cho tất cả Booking thuộc Trip.

**BR-RF-03 [CHỐT]** Booking đã được phục vụ **đến đúng bến xuống dự kiến** trước khi Trip gặp Incident/kết thúc sớm được coi là đã hoàn tất; **không refund vì sự cố xảy ra sau đó**.

**BR-RF-04 [CHỐT]** Booking/đoạn đi **chưa được phục vụ do Trip bị dừng** hoặc **đã bắt đầu nhưng bị cắt giữa chừng** đều được hoàn **100% số tiền đã thanh toán phân bổ cho phần Booking/Ticket đủ điều kiện**. Không phân biệt tỷ lệ refund theo hai nhóm này.

**BR-RF-05 [CHỐT]** **Không có Recovery Voucher 20%, Wallet hoặc hình thức compensation bổ sung** trong scope hiện tại. Không đưa Voucher/VoucherRedemption vào core ERD.

**BR-RF-06 [CHỐT]** NoShow do khách không đến/không check-in đúng hạn **không được hoàn tiền**. Không đánh dấu NoShow đối với hành khách không có cơ hội boarding vì Trip bị Cancelled/Terminated trước khi tới bến lên.

**BR-RF-07 [CHỐT]** Refund phải có trạng thái quá trình (`Requested`, `Processing`, `Succeeded`, `Failed` hoặc trạng thái tương đương), liên kết original payment, có chống lặp và cơ chế retry/reconciliation; được biểu diễn bằng **Refund-type FinancialTransaction**, không yêu cầu bảng Refund độc lập.

**BR-RF-08 [CHỐT]** Nếu Order có hai Booking (round-trip) nhưng chỉ một Booking bị ảnh hưởng, **chỉ hoàn tiền phần được phân bổ cho Booking đủ điều kiện**; Booking còn lại không bị đổi trạng thái chỉ vì cùng Order thanh toán.

**BR-RF-09 [CHỐT]** Trong Booking nhóm, xét điều kiện hoàn tiền theo từng Ticket. Ticket **NoShow do khách bỏ lỡ check-in trước sự cố** không được refund; những Ticket khác cùng Booking nếu chưa được phục vụ hoặc bị gián đoạn do Trip vẫn được **hoàn 100% số tiền đã thanh toán phân bổ cho Ticket đủ điều kiện**. Refund cho Booking bằng tổng paid allocation của các Ticket đủ điều kiện, không bao gồm Ticket NoShow.

**BR-RF-10 [CHỐT]** Nếu gateway không hỗ trợ hoàn tiền tự động về phương thức gốc hoặc trả lỗi không thể xử lý tự động, Admin thực hiện quy trình hoàn tiền thủ công có kiểm soát. Refund-type FinancialTransaction vẫn ở trạng thái Requested/Processing hoặc trạng thái chờ xử lý tương đương; chỉ ghi Succeeded sau khi xác minh khoản hoàn đã thực sự chuyển đến người nhận. Lưu bằng chứng/định danh đối soát, kiểm tra số tiền được hoàn và chống tạo lần hoàn thứ hai khi retry hoặc luồng tự động quay lại.

## 11. Sightseeing và audio qua loa tàu

**BR-EXP-01 [CHỐT]** Sightseeing có Route riêng, POI được cấu hình gắn theo hành trình, có tracking và thuyết minh theo vị trí Boat; Regular có thể cung cấp tracking nhưng không tự động có quyền sử dụng cùng chương trình Sightseeing.

**BR-EXP-02 [CHỐT]** Audio được **phát chung qua hệ thống loa trên Boat**, không phát như một tính năng cá nhân hóa trên điện thoại/web của Passenger. **Một ngôn ngữ tiếng Việt** trong scope hiện tại.

**BR-EXP-03 [CHỐT]** `AudioGuide` thuộc nội dung POI/hành trình và có file nội dung đã chuẩn bị/kiểm duyệt. Một `Onboard Player`/máy tính trên Boat chịu trách nhiệm phát audio ra loa; cloud/backend không được xem là đang trực tiếp phát ra loa nếu không có thiết bị phát kết nối.

**BR-EXP-04 [CHỐT]** Playback của Sightseeing phụ thuộc vào **Trip/Boat/POI** và trạng thái vận hành, **không phụ thuộc từng Ticket đã CheckedIn**. Điều kiện mở khóa audio cá nhân trong BR cũ bị bãi bỏ.

**BR-EXP-05 [CHỐT]** Khi tàu đến vùng POI hợp lệ và Trip đang hoạt động phù hợp, hệ thống có thể kích hoạt playback. Các POI trigger cần ngăn phát trùng không mong muốn trên cùng lượt hành trình.

**BR-EXP-06 [CHỐT]** Khi Trip `Suspended`, `Terminated` hoặc có thông báo an toàn, audio tham quan phải tạm dừng/nhường ưu tiên cho Captain và thông báo an toàn. Không cho automation audio cản trở yêu cầu vận hành an toàn.

**BR-EXP-07 [CHỐT]** Phiên bản môn học không bắt buộc Onboard Player tiếp tục phát thuyết minh theo POI khi mất Internet. Cơ chế cache audio/GPS và playback offline là cải tiến tùy chọn ở thiết kế kỹ thuật; hệ thống không cam kết thuyết minh hoạt động liên tục khi mất kết nối. Việc thông báo an toàn và quyền điều khiển của Captain vẫn được ưu tiên.

## 12. Thông báo, quản trị và báo cáo

**BR-NOT-01 [CHỐT]** Email là kênh thông tin ngoài hệ thống cho Purchaser; realtime Web/In-app phục vụ thay đổi hành trình và Incident khi người dùng đang theo dõi. **Không triển khai SMS.**

**BR-NOT-02 [CHỐT]** Purchaser nhận thông tin giao dịch/hoàn tiền. Passenger/TripAccess nhận nội dung đi tàu được phép xem nhưng không mặc định nhận chi tiết tài chính của Purchaser.

**BR-NOT-03 [CHỐT]** Lỗi gửi email/notification không rollback trạng thái Payment, Booking, Refund hay Trip đã được xác nhận; hệ thống cần khả năng gửi lại/đối soát.

**BR-CFG-01 [CHỐT]** Admin quản lý Route, Station/RouteStop, Schedule, Trip/Boat assignment, Boat/Captain, Seat/SeatClass, fare configuration, cửa sổ bán vé/check-in, nội dung POI/audio và Incident.

**BR-CFG-02 [CHỐT]** Thay đổi cấu hình không làm biến đổi ngược giao dịch hoặc lịch sử vận hành đã hoàn tất. Báo cáo phân biệt **đã bán**, **đã check-in**, **NoShow**, **chuyến bị Cancelled/Terminated** và **tiền thực thu/đã hoàn**.

**BR-AI-01 [CHỐT]** Nếu có AI Assistant, AI đóng vai trò hướng dẫn/truy vấn thông qua API đã phân quyền; AI không trực tiếp bỏ qua các rule nghiệp vụ hoặc tự ý ghi thay đổi thanh toán, check-in hay vận hành đặc quyền.

**BR-AI-02 [NGOÀI PHẠM VI]** AI tự động ra quyết định huỷ tàu, tối ưu đội tàu không cần phê duyệt, hoặc hệ thống phân tích dự báo nâng cao không thuộc bản BR core.

## 13. Các trạng thái nghiệp vụ và quy tắc chuyển trạng thái

| Đối tượng | Trạng thái/cách hiểu được hỗ trợ |
|---|---|
| **Trip** | `Scheduled`, `Boarding`, `EnRoute`, `Suspended`, `Arrived`, `Completed`, `Cancelled`, `Terminated`; `Delayed` là thuộc tính riêng. |
| **Order** | `Draft`, `PendingPayment`, `Confirmed`, `Cancelled`, `Expired` và thông tin tổng hợp refund nếu cần; không dùng Order status để thay thế trạng thái Booking riêng lẻ. |
| **Booking** | `Draft`, `PendingPayment`, `Confirmed`, `Cancelled`, `Terminated`, `Completed`; phải phản ánh riêng phần dịch vụ của Booking. |
| **Ticket** | Vòng đời quyền sử dụng: `Valid`, `Cancelled`, `Terminated`, `Completed` (hoặc tên tương đương). |
| **Ticket.BoardingStatus** | `NotCheckedIn`, `CheckedIn`, `NoShow`. Không có `Boarded` trong scope. |
| **FinancialTransaction** | `TransactionType = Payment/Refund`; trạng thái giao dịch như `Pending`, `Succeeded`, `Failed`, và các trạng thái xử lý/refund chi tiết. |
| **Incident** | Trạng thái xử lý như `Open`, `InProgress`, `Resolved` hoặc tương đương; **không phải** TripStatus. |

**BR-STATE-01 [CHỐT]** Trip chuyển `Scheduled → Boarding → EnRoute → Arrived → Completed` theo diễn biến bình thường; giai đoạn `EnRoute` vẫn bao gồm việc phục vụ check-in tại các bến trung gian.

**BR-STATE-02 [CHỐT]** `Scheduled/Boarding → Cancelled` khi chuyến chưa bắt đầu; `EnRoute → Suspended → EnRoute` nếu khắc phục được; chuyến đang hoạt động có thể kết thúc sớm sang `Terminated` qua quyết định được phân quyền.

**BR-STATE-03 [CHỐT]** Kết thúc đoạn đi của Booking **khác thời điểm** kết thúc Trip toàn tuyến. Booking A→B có thể Completed khi Trip vẫn EnRoute tới C/D; các Ticket cùng Booking cũng phải xét trạng thái cá nhân như NoShow.

**BR-STATE-04 [CHỐT]** Các bước ghi nhận payment/refund/check-in/TripStatus phải có audit, idempotency hoặc cơ chế kiểm soát cạnh tranh phù hợp để không phát vé/hoàn tiền/check-in lặp dẫn đến trạng thái sai.

## 14. Các ví dụ chuẩn để team kiểm thử nghiệp vụ

### Ví dụ A — Regular, check-in tại bến trung gian và reuse ghế

Trip T100: A → B → C → D; Boat V01 có Seat A1 (Standard).

- Booking B001: A→B, Seat A1, Staff tại A check-in hợp lệ.
- Booking B002: B→D, Seat A1, Staff tại B check-in **trước khi Boat tới**, dù Trip T100 có thể đang `EnRoute`.
- Hai Booking cùng ghế được chấp nhận vì đoạn `[A,B)` và `[B,D)` không chồng lấn.
- Nếu Booking khác chọn A→C, Seat A1 thì phải bị từ chối vì overlap.
- Không có bước scan QR lần thứ hai khi lên Boat.

### Ví dụ B — Terminated giữa hành trình

Trip T200: A → B → C → D, đã đến C rồi kết thúc sớm.

| Booking | Đoạn mua | Kết luận theo BR mới |
|---|---|---|
| B101 | A→B | Đã tới đích trước Incident: **không refund**. |
| B102 | B→D | Chưa tới đích do Trip dừng: **refund 100% paid allocation**. |
| B103 | C→D | Không được phục vụ do Trip dừng: **refund 100% paid allocation**. |
| B104 | A→B, khách tự NoShow tại A | Không tự refund vì Incident xảy ra về sau. |

Không phát Voucher 20% cho bất kỳ trường hợp nào.

### Ví dụ C — Round-trip thanh toán chung, refund riêng

Order O001 gồm Booking OUT (A→B, thực trả 300.000đ) và Booking RETURN (C→A, thực trả 200.000đ). Payment TX01 thu 500.000đ thành công. Nếu Trip của RETURN bị Cancelled trước khi chạy, tạo Refund-type FinancialTransaction 200.000đ tham chiếu `Booking RETURN` và `OriginalPayment TX01`; OUT giữ kết quả đã hoàn tất. Các số tiền trong ví dụ chỉ là giả định.

### Ví dụ D — Incident thời tiết trên Route

Incident I01 do WeatherService báo trên Route R01 trong một khung giờ. Admin xét Trip T1 đang chạy, T2 chuẩn bị chạy và T3 chạy muộn hơn. Admin có thể Suspend T1, Cancel T2, chưa tác động T3. I01 không tự động đổi trạng thái của mọi Trip gắn Route.

### Ví dụ E — Sightseeing audio và kiểm soát loại vé

Trip TS01 là Sightseeing Route; Boarding tại bến được cấu hình. Staff scan QR, chọn Ticket hiện diện; hành khách lên tàu theo hướng dẫn. Khi Boat vào vùng POI, thiết bị trên tàu phát AudioGuide tiếng Việt qua loa. Ticket Regular của Trip khác không được dùng để check-in TS01. Khi có thông báo an toàn, audio tham quan bị tạm dừng.

## 15. Định hướng sửa Conceptual ERD từ BR mới

| Thành phần | Quan hệ nghiệp vụ nên biểu diễn |
|---|---|
| Roles | Passenger, Guest, Admin, Staff, Captain là actor/role riêng; **Guest không cần account record**. |
| Route / Station | `Route 1:N RouteStop`; `Station 1:N RouteStop`; Route thuộc loại Regular hoặc Sightseeing. |
| Schedule / Trip | `Route 1:N Schedule`; `Schedule 1:0..N Trip` sinh từ lịch; `Trip 0..1 Schedule` và `Trip 1 Route` kể cả khi ad-hoc. Trip có Schedule phải nhất quán với Route/đoạn chạy của Schedule. |
| Boat / Captain | `Boat 1:1 Captain`; `Boat 1:N Seat`; `Boat 1:N Trip` theo thời gian; Trip phải có Boat trước khi mở bán. |
| Seat / Class | `SeatClass 1:N Seat`; **bỏ SeatLayout**; **không cần TripSeat** trong phương án rút gọn. |
| Order / Booking | `Order 1:N Booking` (nghiệp vụ one-way 1, round-trip 2); `Booking N:1 Trip`; Order account optional để support Guest. |
| SeatReservation / Ticket | `Booking 1:N SeatReservation`, `Seat 1:N SeatReservation` theo thời gian/segment, `SeatReservation 1:0..1 Ticket`. |
| Boarding stops | Booking tham chiếu boarding/disembarking RouteStop thích hợp; bến/giờ dừng theo Trip cần có representation hoặc rule suy diễn rõ. |
| Check-in | `Ticket 1:0..N CheckInEvent` (attempt/history), Staff thực hiện; kết quả hiện hành theo Ticket. |
| FinancialTransaction | `Order 1:N FinancialTransaction`; refund type tham chiếu original payment và Booking bị ảnh hưởng, không bắt buộc `Refund` entity riêng. |
| Incident | Incident có thể liên quan Route, Boat và/hoặc Trip; thêm liên kết nhiều Trip (`IncidentTrip`) nếu cần quản lý mức độ ảnh hưởng từng chuyến. |
| Audio | Route liên kết POI qua quan hệ phù hợp; `POI 1:N AudioGuide` theo nội dung; playback thực thi trên Boat, **không cần Passenger audio access entity**. |

**Lưu ý ranh giới Conceptual/Logical:** Redis CheckoutSession, SQLite offline cache, opaque access tokens, SQL indexes/concurrency, gateway callback history và onboard audio player có thể xuất hiện ở Logical/Technical Design tùy nhu cầu; không nhất thiết mỗi khái niệm kỹ thuật phải là một entity trong Conceptual ERD.

## 16. Các tham số cấu hình sau khi chốt nghiệp vụ

| Mã | Tham số cấu hình | Nguyên tắc đã chốt |
|---|---|---|
| C01 | Giá cụ thể cho mỗi tổ hợp TripType + SeatClass | Admin cấu hình bảng giá; chỉ TripType và SeatClass làm thay đổi đơn giá. |
| C02 | Seat hold TTL và PaymentPending TTL | Admin/hệ thống cấu hình thời lượng; hết hạn theo quy tắc giữ ghế và thanh toán đã chốt. |
| C03 | SalesCloseAt và cửa sổ check-in theo bến | Admin cấu hình mốc/thời lượng theo Trip và điểm dừng; luôn đóng bán trước khi Trip rời bến đầu và check-in theo bến lên. |
| C04 | Hạn dùng, rotation và revocation của guest/passenger secure links | Cấu hình theo chính sách bảo mật; không dùng hằng số nghiệp vụ bất biến. |

## 17. Những rule của bản cũ đã bị thay thế/bãi bỏ

| Nội dung bản cũ | Xử lý ở v2.0 |
|---|---|
| Role Dispatcher tách Admin | **Bỏ Dispatcher**; Admin nhận trách nhiệm điều phối. |
| Captain được gán trực tiếp theo từng Trip | **Captain–Boat 1:1 cố định**; Boat được phân công cho từng Trip. |
| Trip rời bến đầu thì đóng toàn bộ check-in/đánh NoShow mọi người còn lại | **Bãi bỏ**; check-in/NoShow theo bến lên của từng Booking/Ticket. |
| Passenger audio unlock sau check-in, phát trên thiết bị cá nhân | **Bãi bỏ**; audio tiếng Việt phát chung qua loa Boat cho Sightseeing. |
| Seat chỉ một hạng giá | **Thêm SeatClass và pricing theo hạng**. |
| TripSeat là thành phần bắt buộc của ghế được bán | **Ưu tiên bỏ TripSeat**, dùng Seat + SeatReservation, có xử lý thay Boat. |
| SeatLayout có thể giữ lại | **Bỏ hoàn toàn SeatLayout** trong phạm vi hiện tại. |
| WeatherAlert entity riêng là bắt buộc | **Dùng Incident đa nguồn/phạm vi**; WeatherService là nguồn Incident. |
| Refund bắt buộc có entity riêng | **Cho phép dùng chung FinancialTransaction** cho thu và hoàn, có định danh/quan hệ riêng. |
| Terminated mặc định refund cho mọi Booking trên Trip | **Đổi thành refund theo đoạn được phục vụ**: hoàn tất thì không refund; chưa được phục vụ/bị gián đoạn thì 100%. |
| Recovery Voucher 20% khi Terminated | **Bãi bỏ hoàn toàn**; không Wallet/Voucher. |
| Một Route/service chung cho Regular và Sightseeing | **Hai Route dịch vụ riêng**. |

---

**Tuyên bố FINAL:** Các rule [CHỐT] là baseline nghiệp vụ chính thức của Smart Waterbus v2.0 tính đến 29/09/2026. Các mục [CẤU HÌNH] chỉ là tham số vận hành triển khai sau, không phải quyết định nghiệp vụ còn bỏ ngỏ. Những gì [NGOÀI PHẠM VI] không bắt buộc triển khai trong môn học. Khi thay đổi rule phải cập nhật Conceptual ERD, Logical ERD và test cases liên quan.
