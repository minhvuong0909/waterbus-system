# Giao diện báo cáo Doanh thu

Giữ nguyên API `/api/v1/admin/reports/revenue` và cách tính của BE:

- Thực thu: Payment thành công, loại trừ tiền về muộn đang chờ đối soát.
- Đã hoàn: Refund thành công, loại trừ khoản hoàn mô phỏng cũ.
- Thuần: thực thu trừ đã hoàn; cho phép giá trị âm.
- Ngày ghi nhận là `FinancialTransaction.CompletedAt` theo UTC+7, không phải ngày khởi hành.

Giao diện hỗ trợ hôm nay, tháng này, 7/30/90/365 ngày và ngày tùy chọn (tối đa 366 ngày,
gồm cả hai đầu). Kiểm tra ngày trước khi gọi API; đổi đơn vị ngày/tháng giữ nguyên khoảng ngày.
Số liệu tháng chỉ bao gồm các ngày nằm trong khoảng đã chọn, không tự mở rộng sang cả tháng.

Biểu đồ giữ đủ kỳ bằng 0, cột thực thu/hoàn và đường thuần dạng nét đứt, không làm mượt.
Trục tiền gồm mốc 0 và hỗ trợ âm. Với một kỳ duy nhất, đường thuần có chấm để vẫn nhìn thấy.
Không dựng phần trăm tăng trưởng hoặc so sánh kỳ trước khi API chưa cung cấp dữ liệu đó.

Bảng chi tiết sắp xếp mới nhất trước. Mặc định chỉ hiện kỳ có phát sinh; bỏ chọn để đối chiếu cả kỳ 0.
Bộ lọc này không thay đổi tổng hoặc biểu đồ. Bảng phân trang trên tập dữ liệu tối đa 366 kỳ.

CSV tải từ dữ liệu báo cáo đã hoàn tất, gồm tất cả kỳ và số tiền dạng số (kể cả 0/âm), UTF-8 BOM.
Tải CSV bị khóa khi đang nạp để tránh xuất dữ liệu cũ trong lúc thay đổi bộ lọc.
Trạng thái rỗng, lỗi, thử lại và đang cập nhật được hiển thị rõ; bộ lọc vẫn khả dụng khi lỗi.
