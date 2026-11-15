# Quản lý Captain và điểm danh trước chuyến

Các quyết định BR-CAP-01–12 đã được người dùng chốt trong phiên làm việc ngày 16/11/2026. Phần này bổ sung BR v2.0; không thêm Captain thay ca hoặc dự phòng.

| Rule | Quy tắc áp dụng |
|---|---|
| BR-CAP-01 | Chỉ Admin tạo tài khoản Captain; không tự đăng ký. Bắt buộc họ tên, email, điện thoại, số giấy phép duy nhất và ngày hết hạn. Mật khẩu ban đầu tuân theo Identity. |
| BR-CAP-02 | Active/Suspended/Inactive. Khác Active không đăng nhập được; đổi trạng thái thu hồi phiên cũ. Mở lại không phục hồi JWT cũ. |
| BR-CAP-03 | Gán Captain phải tồn tại, đúng role, tài khoản Active và giấy phép còn hạn. Ngày hết hạn tính hết ngày tại Việt Nam. Một Captain tối đa một Boat. |
| BR-CAP-04 | Captain thuộc Boat, Trip lấy Captain qua Boat. Không đổi/gỡ khi Boat có bất kỳ Trip Boarding/EnRoute/Suspended. Thay đổi khi Scheduled đang mở bán tạo cảnh báo lưu trong DB cho Admin. |
| BR-CAP-05 | Không Captain hợp lệ thì không mở bán: Search loại chuyến, detail/seat map báo `captain_unavailable`; booking/checkout/thanh toán kiểm tra lại. Attendance chưa phải điều kiện mua vé. |
| BR-CAP-06 | Captain vắng: Admin đổi Captain của Boat trước Boarding hoặc hủy Trip theo luồng vận hành. Không tự thay ca. |
| BR-CAP-07 | Tạo, sửa hồ sơ, đổi trạng thái, gán/đổi/gỡ và điểm danh lưu audit người thực hiện, thời điểm, dữ liệu cũ/mới và lý do. Không có endpoint xóa Captain. |
| BR-CAP-08 | Captain chỉ truy cập dữ liệu vận hành/điểm danh của Boat và Trip mình; Admin xem toàn bộ. Thông tin tuyến/chuyến công khai cho hành khách vẫn công khai. |
| BR-CAP-09 | **Admin ghi nhận Captain có mặt trực tiếp theo từng Trip**; Captain không tự điểm danh và không điểm danh hộ. Hạn có mặt = `CheckInOpenAt` ở TripStopCall đầu tiên chưa Skipped theo VisitOrder **trừ 20 phút**. Không lấy giờ ở bến trung gian, Station.OrderIndex hoặc mốc giờ cố định. |
| BR-CAP-10 | Quá hạn chưa có mặt: recurring job tạo cảnh báo Admin, chống lặp theo Trip + lần phân công + hạn có mặt. Không tự hủy/Suspend hoặc sửa Trip.Status. |
| BR-CAP-11 | Admin ghi nhận bắt buộc lý do; server tự ghi thời điểm, không nhận giờ điểm danh do client gửi. Request kèm CaptainId và AssignmentVersion để chống xác nhận nhầm sau khi đổi phân công. |
| BR-CAP-12 | Đúng hạn được tính Present; sau hạn ghi Late, vẫn được lưu. Chỉ ghi trước khi xuất bến. Start yêu cầu Captain hiện tại hợp lệ và đã được Admin điểm danh. |

Không cố định giờ Staff bắt đầu thành 06:30 hoặc giờ Captain đến thành 06:10. Cửa sổ Staff vẫn do TripStopCall cấu hình; nếu bắt đầu trước khởi hành 30 phút thì hạn Captain là trước khởi hành 50 phút. Chưa cấu hình CheckInOpenAt bến đầu thì trạng thái điểm danh `NotConfigured`, không tự suy ra giờ. Chưa quy định một mốc sớm nhất để Admin được ghi nhận Captain đã có mặt; API không đặt thêm cửa sổ sớm nhất.

Đổi Captain hoặc đổi Boat làm điểm danh cũ không còn hiệu lực cho phân công mới; lịch sử vẫn giữ. Điểm danh lặp cùng phân công trả lại bản ghi ban đầu, không thay thời điểm/lý do. Bản ghi Late lưu hạn tại thời điểm ghi nhận để báo cáo.

## Hợp đồng API

Mọi URL dưới đây có tiền tố `/api/v1`. Lỗi theo ProblemDetails hiện có. Trạng thái enum theo cấu hình JSON của API. API quản lý cần role Admin; GET attendance cho Admin/Captain, Captain bị giới hạn chuyến được phân công.

| Method | URL | Nội dung |
|---|---|---|
| GET | `/captains?page=1&pageSize=20&search=&status=` | Danh sách phân trang; thông tin giấy phép và BoatId. |
| GET | `/captains/{id}` | Chi tiết Captain. |
| POST | `/captains` | `fullName, email, phone, licenseNumber, licenseExpiresOn` (YYYY-MM-DD), `password`; trả 201 + id. |
| PUT | `/captains/{id}` | Hồ sơ như lúc tạo, bỏ password, thêm reason bắt buộc. |
| PUT | `/captains/{id}/status` | `status, reason`. |
| PUT | `/boats/{boatId}/captain` | `captainId` (null để gỡ), `reason`. API Boat create/update cũ cũng qua cùng kiểm tra và audit. |
| GET | `/trips/{tripId}/captain-attendance` | `tripId, boatId, captainId, assignmentVersion, requiredAt, state, attendance`. |
| POST | `/trips/{tripId}/captain-attendance` | `captainId, assignmentVersion, reason`. Admin xác nhận có mặt; không nhận giờ từ client. |
| GET | `/captains/audit?page=1&pageSize=20&captainId=&tripId=` | Audit phân trang. |
| GET | `/captains/alerts?page=1&pageSize=20&captainId=&tripId=` | Cảnh báo phân trang, gồm thông tin đã xử lý; Admin lấy lại để đồng bộ. |
| POST | `/captains/alerts/{id}/resolve` | Admin đánh dấu đã xử lý; không làm thay đổi Trip. |

`state`: Pending, Missing, Present, Late, NotConfigured. `attendance` gồm Captain, Boat, phiên phân công, người Admin, giờ server ghi nhận, hạn có mặt, isLate và lý do. Không có API tự đăng ký hoặc tự điểm danh Captain. Không thêm quan hệ CaptainId trên Trip.

## Cấu hình và triển khai

`Captain:PreparationMinutes=20`, `Captain:AlertsEnabled=true`, `Captain:AlertCron="* * * * *"`. Docker dùng `CAPTAIN_PREPARATION_MINUTES`, `CAPTAIN_ALERTS_ENABLED`, `CAPTAIN_ALERT_CRON` trong `.env.example`. Job mặc định mỗi phút, nên cảnh báo có thể xuất hiện ở lần chạy tiếp theo sau hạn.

Migration `AddCaptainManagementAndAttendance` chỉ thêm cột và bảng, không xóa dữ liệu. Captain cũ chưa có giấy phép/trạng thái bị xem là Inactive: Admin phải hoàn thiện hồ sơ thật rồi kích hoạt. Không tự tạo giấy phép cho dữ liệu cũ. JWT Captain cũ thiếu claim phiên mới bị từ chối; cần đăng nhập lại. Không ảnh hưởng claim/luồng đăng nhập Passenger, xác minh email hoặc Guest AccessGrant.

JWT Captain được đối chiếu phiên/trạng thái trong DB mỗi request và mỗi lời gọi Hub. Khóa tài khoản ngắt kết nối SignalR trên instance hiện tại. Registry kết nối hiện tại là trong tiến trình, cùng mô hình SignalR của dự án; triển khai nhiều API instance cần cơ chế phát lệnh thu hồi kết nối liên instance để ngắt ngay mọi luồng push đang mở. Không coi registry trong tiến trình là bảo đảm thu hồi tức thời toàn cụm.

Các thay đổi phân công, điểm danh, khóa hồ sơ và hoạt động thương mại dùng chung transaction lock hiện có, để việc kiểm tra Captain không đua với Start hoặc thanh toán. Giữ nguyên vòng đời PurchaseOrder → Booking → SeatReservation → Ticket, Guest linking và email verification. Thanh toán đến khi inventory/Captain không còn hợp lệ đi qua cơ chế reconciliation hiện có, không tự phát vé.
