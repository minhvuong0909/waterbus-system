# Tài liệu Smart Waterbus

## Nguồn chuẩn

- [Business Rules v2.0 FINAL](Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md) — ưu tiên hơn các roadmap/bản đề xuất cũ. File DOCX cùng tên là bản dùng để chia sẻ/in.
- [Quy ước dự án](CONVENTIONS.md), [đối chiếu domain](DOMAIN_BR_ALIGNMENT.md), [Logical ERD](02_Logical_ERD_REVIEWED.dbml).

## Hợp đồng và tích hợp

- [API chi tiết chuyến và sơ đồ ghế](TRIP_READ_API.md).
- [Checkout/payment/refund/QR](NHAT_ANH_COMMERCIAL_API.md), [hướng dẫn test giao dịch](NHAT_ANH_TEST_GUIDE.md).
- [SignalR contract](REALTIME_CONTRACT.md).
- [Lỗi tích hợp còn mở](KNOWN_ISSUES.md) — thay các báo cáo review PR tạm riêng lẻ.
- [Hạ tầng SQL/Redis để chạy test](../waterbus-be/tests/infrastructure/README.md).
- [Tài liệu AI/audio](thinh-ai/README.md).

## Kế hoạch và lịch sử

- [Roadmap](TASK_BREAKDOWN_ROADMAP.md), [chia việc backend](BACKEND_TASK_BREAKDOWN.md): kế hoạch lịch sử, không thay thế BR FINAL hoặc trạng thái code hiện tại.
- [Rà soát ERD](05_AUDIT_REPORT_VI.md), [báo cáo TV6](TV6_KIET_REPORT.md).
- [Bản đề xuất BR v1.0](archive/Smart_Waterbus_Business_Rules_Proposed_v1_0.docx): đã được v2.0 thay thế, chỉ giữ để tra cứu lịch sử.

Không xóa tài liệu chuyên môn/nguồn đối chiếu của thành viên chỉ vì không được import trong source code.
