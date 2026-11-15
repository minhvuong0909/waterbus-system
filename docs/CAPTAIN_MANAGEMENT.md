# Quản lý Captain và điểm danh trước chuyến

Các quyết định BR-CAP-01–12 đã được người dùng chốt trong phiên làm việc ngày 16/11/2026. Phần này bổ sung BR v2.0; không thêm Captain thay ca hoặc dự phòng.

| Rule | Quy tắc áp dụng |
|---|---|
| BR-CAP-01 | Chỉ Admin tạo tài khoản Captain; không tự đăng ký. Bắt buộc họ tên, email, điện thoại, số giấy phép duy nhất và ngày hết hạn. Admin không nhập mật khẩu; người nhận dùng email kích hoạt một lần để tự đặt mật khẩu theo Identity. |
| BR-CAP-02 | Active/Suspended/Inactive. Khác Active không đăng nhập được; đổi trạng thái thu hồi phiên cũ. Mở lại không phục hồi JWT cũ. |
| BR-CAP-03 | Gán Captain phải tồn tại, đúng role, tài khoản Active và giấy phép còn hạn. Ngày hết hạn tính hết ngày tại Việt Nam. Một Captain tối đa một Boat. |
| BR-CAP-04 | Captain thuộc Boat, Trip lấy Captain qua Boat. Không đổi/gỡ khi Boat có bất kỳ Trip Boarding/EnRoute/Suspended. Thay đổi khi Scheduled đang mở bán tạo cảnh báo lưu trong DB cho Admin. |
| BR-CAP-05 | Không Captain hợp lệ thì không mở bán: Search loại chuyến, detail/seat map báo `captain_unavailable`; booking/checkout/thanh toán kiểm tra lại. Attendance chưa phải điều kiện mua vé. |
| BR-CAP-06 | Captain vắng: Admin đổi Captain của Boat trước Boarding hoặc hủy Trip theo luồng vận hành. Không tự thay ca. |
| BR-CAP-07 | Tạo, sửa hồ sơ, đổi trạng thái, gán/đổi/gỡ và điểm danh lưu audit người thực hiện, thời điểm, dữ liệu cũ/mới và lý do. Không có endpoint xóa Captain. |
| BR-CAP-08 | Captain chỉ truy cập dữ liệu vận hành/điểm danh của Boat và Trip mình; Admin xem toàn bộ. Thông tin tuyến/chuyến công khai cho hành khách vẫn công khai. |
| BR-CAP-09 | **Admin ghi nhận Captain có mặt trực tiếp theo từng Trip**; Captain không tự điểm danh và không điểm danh hộ. Hạn có mặt = `CheckInOpenAt` ở TripStopCall đầu tiên chưa Skipped theo VisitOrder **trừ 20 phút**. Không lấy giờ ở bến trung gian, Station.OrderIndex hoặc mốc giờ cố định. |
| BR-CAP-10 | Quá hạn chưa có mặt: recurring job tạo cảnh báo Admin, chống lặp theo Trip + lần phân công. Đổi giờ không tạo thêm cảnh báo cho cùng phân công. Không tự hủy/Suspend hoặc sửa Trip.Status. |
| BR-CAP-11 | Admin ghi nhận bắt buộc lý do; server tự ghi thời điểm, không nhận giờ điểm danh do client gửi. Request kèm CaptainId và AssignmentVersion để chống xác nhận nhầm sau khi đổi phân công. |
| BR-CAP-12 | Đúng hạn được tính Present; sau hạn ghi Late, vẫn được lưu. Chỉ ghi trước khi xuất bến. Start yêu cầu Captain hiện tại hợp lệ và đã được Admin điểm danh. |

Không cố định giờ Staff bắt đầu thành 06:30 hoặc giờ Captain đến thành 06:10. Cửa sổ Staff vẫn do TripStopCall cấu hình; nếu bắt đầu trước khởi hành 30 phút thì hạn Captain là trước khởi hành 50 phút. Chưa cấu hình CheckInOpenAt bến đầu thì trạng thái điểm danh `NotConfigured`, không tự suy ra giờ. Chưa quy định một mốc sớm nhất để Admin được ghi nhận Captain đã có mặt; API không đặt thêm cửa sổ sớm nhất.

Đổi Captain hoặc đổi Boat làm điểm danh cũ không còn hiệu lực cho phân công mới; lịch sử vẫn giữ. Điểm danh lặp cùng phân công trả lại bản ghi ban đầu, không thay thời điểm/lý do. Bản ghi Late lưu hạn tại thời điểm ghi nhận để báo cáo.

## Hợp đồng API

Mọi URL dưới đây có tiền tố `/api/v1`. Lỗi theo ProblemDetails hiện có. Trạng thái enum theo cấu hình JSON của API. API quản lý cần role Admin; GET attendance cho Admin/Captain, Captain bị giới hạn chuyến được phân công.

| Method | URL | Nội dung |
|---|---|---|
| GET | `/captains?page=1&pageSize=20&search=&status=` | Danh sách phân trang; thông tin giấy phép, BoatId, invitationPending và emailConfirmed để Admin biết cần mời lại hay cập nhật hồ sơ. |
| GET | `/captains/{id}` | Chi tiết Captain. |
| POST | `/captains` | `fullName, email, phone, licenseNumber, licenseExpiresOn` (YYYY-MM-DD); không có password. Trả 201 + id, tài khoản Inactive và xếp hàng email kích hoạt. |
| PUT | `/captains/{id}` | Hồ sơ như lúc tạo, thêm reason bắt buộc. Đổi email thu hồi phiên, ngừng đăng nhập và yêu cầu kích hoạt email mới. Suspended phải chuyển Inactive trước khi đổi email. |
| POST | `/captains/{id}/invitation` | `reason` bắt buộc; gửi lại lời mời, vô hiệu link cũ. Suspended phải chuyển Inactive trước khi mời lại. |
| PUT | `/captains/{id}/status` | `status, reason`. |
| PUT | `/boats/{boatId}/captain` | `captainId` (null để gỡ), `reason`. Boat create/update cũ cũng qua cùng kiểm tra và audit; cần `captainChangeReason` khi thực sự gán/đổi/gỡ Captain. |
| GET | `/trips/{tripId}/captain-attendance` | `tripId, boatId, captainId, assignmentVersion, requiredAt, state, attendance`. |
| POST | `/trips/{tripId}/captain-attendance` | `captainId, assignmentVersion, reason`. Admin xác nhận có mặt; không nhận giờ từ client. |
| GET | `/captains/audit?page=1&pageSize=20&captainId=&tripId=` | Audit phân trang. |
| GET | `/captains/alerts?page=1&pageSize=20&captainId=&tripId=` | Cảnh báo phân trang, gồm thông tin đã xử lý; Admin lấy lại để đồng bộ. |
| POST | `/captains/alerts/{id}/resolve` | Admin đánh dấu đã xử lý; không làm thay đổi Trip. |

`state`: Pending, Missing, Present, Late, NotConfigured. `attendance` gồm Captain, Boat, phiên phân công, người Admin, giờ server ghi nhận, hạn có mặt, isLate và lý do. Không có API tự đăng ký hoặc tự điểm danh Captain. Không thêm quan hệ CaptainId trên Trip.

## Cấu hình và triển khai

`PersonnelInvitation:TokenLifetimeHours=24` (tối đa 168 giờ), `Captain:AlertBatchSize=100` (1–1000), `Captain:PreparationMinutes=20`, `Captain:AlertsEnabled=true`, `Captain:AlertCron="* * * * *"`. Docker dùng các biến `PERSONNEL_INVITATION_TOKEN_LIFETIME_HOURS`, `CAPTAIN_ALERT_BATCH_SIZE`, `CAPTAIN_PREPARATION_MINUTES`, `CAPTAIN_ALERTS_ENABLED`, `CAPTAIN_ALERT_CRON` trong `.env.example`. Job mặc định mỗi phút, nên cảnh báo có thể xuất hiện ở lần chạy tiếp theo sau hạn.

Migration `AddCaptainManagementAndAttendance` chỉ thêm cột và bảng, không xóa dữ liệu. Captain cũ chưa có giấy phép/trạng thái bị xem là Inactive: Admin phải hoàn thiện hồ sơ thật rồi kích hoạt. Không tự tạo giấy phép cho dữ liệu cũ. JWT Captain cũ thiếu claim phiên mới bị từ chối; cần đăng nhập lại. Không ảnh hưởng claim/luồng đăng nhập Passenger, xác minh email hoặc Guest AccessGrant.

JWT Captain được đối chiếu phiên/trạng thái trong DB mỗi request và mỗi lời gọi Hub. Khóa tài khoản ngắt kết nối SignalR trên instance hiện tại. Registry kết nối hiện tại là trong tiến trình, cùng mô hình SignalR của dự án; triển khai nhiều API instance cần cơ chế phát lệnh thu hồi kết nối liên instance để ngắt ngay mọi luồng push đang mở. Không coi registry trong tiến trình là bảo đảm thu hồi tức thời toàn cụm.

Các thay đổi phân công, điểm danh, khóa hồ sơ và hoạt động thương mại dùng chung transaction lock hiện có, để việc kiểm tra Captain không đua với Start hoặc thanh toán. **Job cảnh báo dùng khóa riêng** `waterbus:captain-attendance`, không chờ khóa thương mại. Một lệnh SQL lấy tối đa AlertBatchSize chuyến thực sự quá hạn, Scheduled/Boarding; lấy bến đầu chưa Skipped theo VisitOrder và kiểm tra attendance/phân công ngay trong SQL. Worker không lấy được khóa riêng thì bỏ lượt; khóa và unique dedup key chống trùng. Job không sửa Trip, không truy vấn Booking/Payment. READPAST bỏ qua Trip/Boat đang bị khóa; lượt sau xét lại. Giữ nguyên vòng đời PurchaseOrder → Booking → SeatReservation → Ticket, Guest linking và email verification. Thanh toán đến khi inventory/Captain không còn hợp lệ đi qua cơ chế reconciliation hiện có, không tự phát vé.

Chuyến vẫn Scheduled/Boarding dù đã qua giờ cập bến dự kiến vẫn cần Admin xử lý; job không âm thầm loại chuyến đó theo ArrivalTime. Lịch sử Completed/Cancelled/Terminated không thuộc tập xét cảnh báo. Index theo Status giới hạn tập truy vấn, batch và điều kiện đến hạn được thực thi tại SQL.

## Kích hoạt tài khoản nhân sự

Tái sử dụng UserManager, ASP.NET Identity Data Protection và IVerificationEmailQueue hiện có cho Captain/Staff; không tạo kho mật khẩu hoặc token riêng. Provider PersonnelInvitation có purpose riêng, thời hạn cấu hình và security stamp. Resend quay stamp, link cũ không dùng lại được. ResetPassword xoay stamp và bỏ cờ pending trong transaction, nên hai lần kích hoạt đồng thời chỉ một lần thành công. Không đưa mật khẩu/token vào audit hoặc DB dạng plaintext. Data Protection keys phải bền vững, dùng chung giữa các instance theo cấu hình triển khai hiện có.

Link là `/verify-email#uid=...&token=...`; dùng lại form FE và `POST /api/v1/auth/confirm-email {userId,token,password}` hiện có (204). Passenger verification giữ nguyên nhánh cũ. Captain kích hoạt thành công có audit với `actedByCaptainId`; `actedByAdminId` null đúng actor, không gán giả Admin. Admin không thể Active tài khoản chưa xác minh email/chưa đặt mật khẩu. Cần bật Email và cấu hình SMTP/PublicBaseUrl hợp lệ trước khi tạo hoặc mời lại.

Queue email hiện có nằm trong bộ nhớ. Queue đầy hoặc SMTP lỗi không đồng nghĩa đã giao email; tài khoản vẫn chưa hoạt động. Admin tìm tài khoản theo email và gửi lại lời mời, không tạo bản sao. Email không nằm trong transaction DB: lỗi sau khi tạo có thể trả 409 nhưng đã có tài khoản Inactive; API resend là đường khôi phục. Không khẳng định SMTP đã giao thư chỉ từ HTTP 201.

## Nâng cấp dữ liệu cũ và chốt mở bán

1. Sao lưu và diễn tập trên bản sao database hiện hữu. Lập danh sách Trip đang bán, Captain thiếu hồ sơ và PurchaseOrder Draft/PendingPayment. Thông báo Admin trước khi chuyển lưu lượng; không triển khai eligibility guard vào public traffic rồi mới phát hiện thiếu giấy phép.
2. Chọn cửa sổ bảo trì có kiểm soát: dừng tạo hold/payment mới ở lớp triển khai, giữ IPN/reconciliation của bản đang chạy để xử lý giao dịch chờ. Không kéo dài TTL, không xóa Booking/AccessGrant/FinancialTransaction. Đóng băng thao tác phân công trong giai đoạn chuyển phiên bản.
3. Chạy migration có sẵn rồi `HardenCaptainInvitationsAndAttendance`. Migration không điền giấy phép, không đổi trạng thái Captain cũ hoặc giao dịch cũ; tạo `CaptainProfileRequired` bền vững cho Trip có hồ sơ không hợp lệ và còn lịch vận hành/giao dịch chờ. Chạy update lần hai phải không thay đổi dữ liệu. Downgrade bị chặn khi có invitation pending hoặc audit tự kích hoạt để bảo toàn dữ liệu.
4. Chạy instance mới chỉ cho Admin trong mạng nội bộ; dùng PUT hồ sơ với giấy phép thật đã kiểm tra, lý do rõ ràng, rồi Active nếu đã xác minh email/có mật khẩu. Captain chưa kích hoạt dùng invitation và tự đặt mật khẩu. Không tạo giấy phép mẫu để qua gate. Tài khoản bị Suspended phải được Admin xét chuyển Inactive trước khi mời.
5. Chạy [CaptainSalesCutoverPreflight.sql](sql/CaptainSalesCutoverPreflight.sql) bằng `sqlcmd -b` trên database đích. Script chỉ đọc, trả Trip/Boat/Captain ID và cờ pending; THROW 51013 nếu còn blocker. Đây là **gate vận hành bắt buộc**, không phải kiểm tra tự chạy lúc startup. Nếu chưa sửa được, giữ cửa sổ bảo trì/phiên bản cũ và để Admin quyết định chuyến/giao dịch; không mở public traffic mới.
6. Kiểm tra lại từng cảnh báo với Admin, test mua vé và IPN/reconciliation trong môi trường nghiệm thu rồi mới chuyển lưu lượng. TTL/giao dịch đã hết hạn vẫn theo rule hiện có. Callback không đủ điều kiện không tự phát vé; theo dõi reconciliation. Không đánh dấu cảnh báo resolved trước khi xử lý xong.

## Phạm vi FE tách riêng

PR này chỉ Backend và tài liệu contract. FE Admin riêng cần: danh sách/chi tiết/hồ sơ Captain; tạo và gửi lại invitation (không ô mật khẩu Admin); đổi trạng thái có lý do; phân công Boat có lý do và xử lý 409; điểm danh theo Trip với captainId + assignmentVersion mới lấy từ GET; hiện giờ Việt Nam, Present/Late/Missing/NotConfigured; cảnh báo và resolve; audit phân biệt Admin và Captain tự kích hoạt. Poll GET alerts để đồng bộ; chưa thêm event SignalR cho cảnh báo. Không redesign Admin hoặc sửa Scanner/Sightseeing trong PR này.
