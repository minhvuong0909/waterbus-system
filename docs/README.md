# 📚 Trung Tâm Tài Liệu & Đặc Tả Kỹ Thuật (Smart Waterbus Docs)

Thư mục chứa toàn bộ tài liệu nghiệp vụ chuẩn, hợp đồng API liên module, kiến trúc CSDL và hướng dẫn tích hợp hệ thống **Smart Waterbus**.

---

## 🏛️ 1. Nguồn Chuẩn Nghiệp Vụ & Kiến Trúc (Core Baseline)

| Tài liệu | Mục đích & Ý nghĩa | Mức độ ưu tiên |
|---|---|---|
| [**Business Rules v2.0 FINAL**](Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md) | **Quy tắc nghiệp vụ chuẩn hóa cao nhất của toàn bộ hệ thống** (29/09/2026). Bản gốc bằng văn bản quyết định mọi hành vi về vé, ghế, tiền và sự cố. *(Bản DOCX cùng tên dùng để in ấn/bảo vệ đồ án)*. | 🔴 Bắt buộc tuân thủ tuyệt đối |
| [**Quy Ước Dự Án (CONVENTIONS.md)**](CONVENTIONS.md) | Quy chuẩn chi tiết về cấu trúc mã nguồn, naming conventions, xử lý lỗi, kiểm thử và bảo mật. | 🔴 Bắt buộc tuân thủ |
| [**Logical ERD Reviewed**](02_Logical_ERD_REVIEWED.dbml) | Thiết kế cơ sở dữ liệu chi tiết gồm 33 thực thể, quan hệ ràng buộc, khóa chính, khóa ngoại và chỉ mục. | 🟡 Chuẩn thiết kế CSDL |

---

## 🔌 2. Hợp Đồng API & Tích Hợp Liên Module (Contracts & Integration)

| Hợp đồng | Phân hệ liên quan | Nội dung chính |
|---|---|---|
| [**TRIP_READ_API.md**](TRIP_READ_API.md) | Backend ⇄ Frontend | Hợp đồng API chi tiết chuyến tàu (`/trips/{id}`) và sơ đồ ghế trống theo đoạn hành trình (`/trips/{id}/seat-availability`). |
| [**NHAT_ANH_COMMERCIAL_API.md**](NHAT_ANH_COMMERCIAL_API.md) | Backend ⇄ Frontend / Mobile | Đặc tả giao dịch thương mại: Checkout giữ chỗ, tạo đơn, xác thực thanh toán VNPay, chữ ký số QR RSA và yêu cầu hoàn tiền. |
| [**NHAT_ANH_TEST_GUIDE.md**](NHAT_ANH_TEST_GUIDE.md) | QA / Backend Developers | Hướng dẫn kiểm thử giao dịch tài chính, phân bổ hoàn tiền và bảo vệ chống Double-booking. |
| [**REALTIME_CONTRACT.md**](REALTIME_CONTRACT.md) | Backend ⇄ Frontend Web | Đặc tả SignalR Hubs (`/hubs/seat-sync`, `/hubs/boat-tracking`), định dạng sự kiện và cơ chế join group. |
| [**Tài Liệu Module AI (thinh-ai/)**](thinh-ai/README.md) | AI Developer | Tổng hợp tài liệu Semantic Kernel, Azure Speech TTS, trắc địa Geofence POI và hướng dẫn cấu hình key. |
| [**Lỗi Tích Hợp Còn Mở (KNOWN_ISSUES.md)**](KNOWN_ISSUES.md) | Toàn đội ngũ | Theo dõi các lỗi kỹ thuật và các điểm cần đồng bộ liên module còn tồn đọng. |
| [**Hạ Tầng SQL/Redis Test**](../waterbus-be/tests/infrastructure/README.md) | DevOps / Testing | Hướng dẫn chạy kiểm thử tích hợp trên container Docker SQL Server 2022 và 3 node Redis độc lập. |

---

## 📜 3. Tài Liệu Lịch Sử & Báo Cáo Thành Viên (Historical & Reports)

*Các tài liệu sau đây phản ánh quá trình triển khai ban đầu hoặc báo cáo đánh giá đồ án, không dùng để ghi đè Business Rules v2.0 FINAL*:

- [**Báo Cáo Kỹ Thuật TV6 (TV6_KIET_REPORT.md)**](TV6_KIET_REPORT.md): Báo cáo tiến độ phân hệ Real-time, DevOps & Background Jobs của Kiệt.
- [**Đề Xuất BR v1.0 Cũ (archive/Smart_Waterbus_Business_Rules_Proposed_v1_0.docx)**](archive/Smart_Waterbus_Business_Rules_Proposed_v1_0.docx): Bản thảo cũ, lưu trữ làm bằng chứng tiến độ đồ án.

> ⚠️ **Lưu ý bảo vệ tài liệu**: Tuyệt đối không xóa tài liệu chuyên môn hoặc báo cáo đóng góp của các thành viên trong thư mục `docs/`.
