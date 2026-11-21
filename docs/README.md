# Tài liệu Smart Waterbus

Mục lục toàn bộ tài liệu. Về cách chạy dự án, xem [README gốc](../README.md).

> **Thứ tự ưu tiên khi tài liệu mâu thuẫn:** Business Rules v2.0 → [AGENTS.md](../AGENTS.md) → [CONVENTIONS.md](CONVENTIONS.md) → các tài liệu còn lại. `SETUP.md` ở thư mục gốc viết trước BR v2.0 nên chỉ để tham khảo lịch sử.

## 1. Nền tảng (đọc trước)

| Tài liệu | Nội dung |
|---|---|
| [Business Rules v2.0 FINAL](Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md) | Quy tắc nghiệp vụ chuẩn: vé, ghế, tiền, sự cố. Có bản [DOCX](Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.docx) để in |
| [CONVENTIONS.md](CONVENTIONS.md) | Cấu trúc mã nguồn, đặt tên, xử lý lỗi, kiểm thử, bảo mật |
| [02_Logical_ERD_REVIEWED.dbml](02_Logical_ERD_REVIEWED.dbml) | Thiết kế CSDL (entity, quan hệ, khoá, chỉ mục) |
| [ACCOUNT_ACCESS_MODEL.md](ACCOUNT_ACCESS_MODEL.md) | Mô hình tài khoản và quyền truy cập (Passenger, Guest, Staff...) |

## 2. Hợp đồng API và tích hợp

| Tài liệu | Phạm vi |
|---|---|
| [TRIP_READ_API.md](TRIP_READ_API.md) | Chi tiết chuyến và sơ đồ ghế theo đoạn |
| [NHAT_ANH_COMMERCIAL_API.md](NHAT_ANH_COMMERCIAL_API.md) | Checkout, thanh toán VNPay, QR ký số, hoàn tiền |
| [REFUND_MANUAL_MODE.md](REFUND_MANUAL_MODE.md) | Chế độ hoàn tiền thủ công |
| [REALTIME_CONTRACT.md](REALTIME_CONTRACT.md) | SignalR Hubs, sự kiện, nhóm |
| [API_CLIENT_INTEGRATION.md](API_CLIENT_INTEGRATION.md) | Cách FE/mobile gọi API |
| [STAFF_ACCOUNTS.md](STAFF_ACCOUNTS.md) | Tài khoản nhân viên (Staff) |
| [CAPTAIN_MANAGEMENT.md](CAPTAIN_MANAGEMENT.md), [CAPTAIN_TRIP_FLOW.md](CAPTAIN_TRIP_FLOW.md) | Quản lý thuyền trưởng, điểm danh, luồng chuyến |
| [STATION_SCANNER_WORKFLOW.md](STATION_SCANNER_WORKFLOW.md) | Luồng soát vé tại bến |
| [ADMIN_REVENUE_UI.md](ADMIN_REVENUE_UI.md) | Tab Doanh thu của Admin |

## 3. AI, Audio và Email

| Tài liệu | Nội dung |
|---|---|
| [thinh-ai/README.md](thinh-ai/README.md) | Mục lục module AI Booking Assistant, Azure TTS, geofence |
| [SIGHTSEEING_AUDIO_PLAYBACK.md](SIGHTSEEING_AUDIO_PLAYBACK.md) | Audio trên tàu: thông báo bến và thuyết minh địa danh |
| [DEMO_AUDIO.md](DEMO_AUDIO.md) | Các bước demo audio |
| [EMAIL_NOTIFICATION_IMPLEMENTATION_PLAN.md](EMAIL_NOTIFICATION_IMPLEMENTATION_PLAN.md) | Thiết kế email giao dịch; mẫu email ở [email-samples/](email-samples/) |

## 4. Kiểm thử và vận hành

| Tài liệu | Nội dung |
|---|---|
| [NHAT_ANH_TEST_GUIDE.md](NHAT_ANH_TEST_GUIDE.md) | Kiểm thử giao dịch tài chính, hoàn tiền, chống đặt trùng |
| [TEST_CHECKLIST_ROLES.md](TEST_CHECKLIST_ROLES.md) | Checklist kiểm thử theo vai trò |
| [DEMO_CHECKLIST_2026-10-24.md](DEMO_CHECKLIST_2026-10-24.md) | Checklist demo (24/10/2026) |
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Lỗi và điểm cần đồng bộ còn mở |
| [sql/](sql/) | Script SQL hỗ trợ (kiểm tra trước khi cutover) |
| [Hạ tầng test SQL/Redis](../waterbus-be/tests/infrastructure/README.md) | Chạy test tích hợp trên Docker |

## 5. Lịch sử và báo cáo

Không dùng để ghi đè Business Rules v2.0.

| Tài liệu | Nội dung |
|---|---|
| [C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md](C3_REFUND_TIMEOUT_DECISION_PROPOSAL.md) | Đề xuất quyết định timeout hoàn tiền |
| [TV6_KIET_REPORT.md](TV6_KIET_REPORT.md) | Báo cáo Real-time, DevOps, Background Jobs |
| [archive/](archive/) | Bản thảo Business Rules v1.0 |

> Thư mục `docs/` giữ báo cáo và tài liệu chuyên môn của thành viên, đừng xoá khi chưa hỏi nhóm. Muốn gỡ file thì dùng `git rm` trong PR riêng để còn truy lại được lịch sử.
