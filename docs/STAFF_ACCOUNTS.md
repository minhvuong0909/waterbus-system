# Quản lý tài khoản Staff và Captain (Admin)

FE: nhóm **Nhân sự** ở sidebar Admin, gồm `/admin/captains` (Thuyền trưởng) và `/admin/staff-accounts` (Tài khoản nhân viên). Quy tắc Captain (BR-CAP-01–12) xem `CAPTAIN_MANAGEMENT.md`.

## API Staff (chỉ Admin, `/api/v1/staff-accounts`)

| Method | URL | Nội dung |
|---|---|---|
| GET | `?page=1&pageSize=20&search=&isActive=` | Danh sách phân trang; `search` khớp tên, email hoặc mã nhân viên. |
| GET | `/{id}` | Chi tiết. 404 nếu id không phải tài khoản Staff. |
| POST | `/` | `fullName, email, phone, staffCode?`. Không có mật khẩu: tài khoản tạo ra **chưa hoạt động**, email kích hoạt được gửi, nhân viên tự đặt mật khẩu. Trả 201 + id. |
| PUT | `/{id}` | Sửa hồ sơ. Đổi email: tắt đăng nhập, vô hiệu link mời cũ, gửi link mới tới email mới. |
| PUT | `/{id}/status` | `{ isActive }`. Chỉ bật được tài khoản đã kích hoạt (409 nếu chưa). |
| POST | `/{id}/invitation` | `{ reason }` bắt buộc; gửi lại lời mời, link cũ hết hiệu lực. |

Dùng chung `IPersonnelInvitationService` với Captain (cùng token một lần, link `/verify-email#uid=...&token=...`). Không có API xoá. `staffCode` được chuẩn hoá in hoa và phải duy nhất (409 nếu trùng).

Giới hạn đã biết: tắt đăng nhập chặn đăng nhập mới nhưng JWT đã cấp vẫn dùng được tới khi hết hạn (Staff chưa có phiên bản phiên như Captain). Chưa có bảng audit riêng cho Staff (không cần migration); lý do gửi lại lời mời chỉ đi vào email, không lưu.

Captain dùng API có sẵn `/api/v1/captains` (list/tạo/sửa/đổi trạng thái/mời lại). `CaptainStatus` được API trả và nhận dạng **số** (1 Active, 2 Suspended, 3 Inactive).

## Giao diện Admin (nhóm Nhân sự)

- **Thuyền trưởng** (`/admin/captains`): ba tab. *Thuyền trưởng*: tạo, sửa (có lý do), mời lại, đổi trạng thái (có lý do), gán/đổi/gỡ tàu (`PUT /boats/{id}/captain`, có lý do). *Cảnh báo*: `GET /captains/alerts` và đánh dấu đã xử lý. *Nhật ký hoạt động*: `GET /captains/audit`.
- **Điểm danh Captain theo chuyến**: trang *Chuyến* → Thao tác → Điểm danh thuyền trưởng (`GET/POST /trips/{id}/captain-attendance`). Form gửi lại `assignmentVersion` vừa đọc; giờ ghi nhận do server đặt.
- **Tài khoản nhân viên** (`/admin/staff-accounts`): tạo, sửa, mời lại, bật/tắt đăng nhập.
- **Máy quét**: ngoài đăng ký/ghép đôi còn đổi nhãn máy (`PUT /admin/scanner-devices/{id}`).

