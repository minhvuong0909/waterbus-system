# Smart Waterbus — Passenger Web: kế hoạch branch theo feature

Nguồn: bản export Stitch (.zip), gồm 15 màn hình + logo + tài liệu design system.
Mỗi màn hình trong export là một file `code.html` (Tailwind CDN, font Plus Jakarta Sans + Inter, icon Material Symbols) kèm `screen.png`.

## 1. Bảng màn hình → branch

| # | Màn hình (thư mục trong export) | Branch | Nội dung chính |
|---|---|---|---|
| 1 | design system, logo, header, footer | `feature/foundation` | Token màu/font, Tailwind config, layout chung, component cơ bản |
| 2 | account_access_google_auth_streamlined | `feature/auth` | Đăng nhập, tạo tài khoản, Continue with Google, Guest Ticket Lookup |
| 3 | home_urban_river_flow | `feature/home` | Hero, tính năng nổi bật, Next Departures, tuyến (Line 1 Express, Line 2 Heritage, Sunset), How It Works |
| 4 | search_journey_river_corridor_network | `feature/trip-search` | Form tìm chuyến: One-way / Round-trip, đổi điểm đi/đến, gợi ý tuyến |
| 5 | search_results_river_corridor_departures | `feature/trip-search` | Danh sách chuyến theo ngày (từ 15.000 VND), lọc giờ, Select Trip, hỏi Pier Concierge |
| 6 | trip_detail_streamlined | `feature/trip-search` | Journey Timeline, tiện ích trên tàu, Boarding & E-Ticket, Fare Summary, Choose Seat |
| 7 | seat_selection_river_catamaran_cabin | `feature/booking-flow` | Sơ đồ ghế boong chính, gợi ý ghế, Booking Summary |
| 8 | passenger_details_streamlined_single_passenger | `feature/booking-flow` | Thông tin hành khách (Adult/Senior/Child), liên hệ nhận vé, hỗ trợ lên tàu |
| 9 | checkout_review_booking | `feature/booking-flow` | Xem lại đặt vé, sửa chuyến/ghế/hành khách, mã giảm giá (Apply) |
| 10 | payment_streamlined_verified | `feature/payment` | Chọn phương thức, quét QR ngân hàng, Order Summary, "I've Completed Payment" |
| 11 | booking_success_refined_confirmation | `feature/payment` | Xác nhận thành công, mã đặt chỗ (Copy), bước tiếp theo, View My Ticket, Track This Trip |
| 12 | my_tickets_journey_wallet | `feature/my-tickets` | Ví vé: Upcoming / Past, View Full Ticket, Track Live Vessel |
| 13 | ticket_detail_boarding_pass_qr | `feature/my-tickets` | Vé chi tiết, QR phóng to, In / Lưu PDF, hướng dẫn lên tàu |
| 14 | manage_booking_passenger_self_service | `feature/my-tickets` | Đổi chuyến, yêu cầu hoàn tiền, voucher, hỗ trợ nhanh (Refund Help, Explain Voucher, Trip Status) |
| 15 | live_trip_tracking_sightseeing_river_exploration | `feature/live-tracking` | Bản đồ theo dõi thời gian thực, zoom/định vị, điểm dọc tuyến, audio tour đa ngôn ngữ (EN/VI/FR/JP) |
| 16 | explore_refined_river_journeys | `feature/explore` | Hành trình ngắm cảnh, so sánh transit vs sightseeing, lọc POI, câu chuyện dọc sông, audio |

Ghi chú: một branch có thể gồm nhiều màn hình khi các màn hình đó chia sẻ state hoặc component (ví dụ chuỗi đặt vé).

## 2. Thứ tự làm và merge đề xuất

1. `feature/foundation` (làm trước, mọi branch khác dựa trên nó)
2. `feature/home`, `feature/auth` (độc lập, có thể song song)
3. `feature/trip-search` (tìm chuyến → kết quả → chi tiết chuyến)
4. `feature/booking-flow` (ghế → hành khách → xem lại)
5. `feature/payment` (thanh toán → xác nhận)
6. `feature/my-tickets` (ví vé, vé chi tiết, quản lý đặt vé)
7. `feature/live-tracking`
8. `feature/explore`

## 3. Phần dùng chung nên nằm trong `feature/foundation`

- **Design tokens**: màu (Deep River #0D2538, Teal Flow #147A7E, Sky Aqua #4FC3D8, Mist #F4F7F8, Sand Light #E9F0EC, Coral Glow #F08A6B, Signal Amber #E8B63E) và typography trong `DESIGN.md`.
- **Header**: menu Explore / Routes / Experience / About + nút Book a Trip. Hiện chưa thống nhất giữa các màn hình (Home có "Sign in", Manage Booking có "Account", có màn có breadcrumb riêng).
- **Footer**: trong export có ít nhất 3 phiên bản khác nhau (nhóm "Book a Ticket / Schedules & Piers", nhóm "Pier Network & Stations", nhóm "Routes & Lines"). Nên chọn một bản và dùng chung.
- **Logo** (SVG), **icon** Material Symbols, component cơ bản (button, card, badge, input).

## 4. Component lặp lại giữa các branch

- Thẻ tóm tắt chuyến/giá (Crossing Summary, Booking Summary, Order Summary, Fare Summary): dùng ở trip-detail, seat-selection, passenger-details, checkout, payment → tạo một component ở `feature/booking-flow` hoặc `foundation`.
- Thẻ vé có QR: dùng ở booking-success, my-tickets, ticket-detail, live-tracking → tạo ở `feature/my-tickets`, các branch sau dùng lại.
- Bản đồ: home, search-journey, live-tracking, explore.
- Audio player + chọn ngôn ngữ: explore và live-tracking.
- Nút Copy (mã đặt chỗ, mã thanh toán): booking-success, payment, ticket-detail.

## 5. Điểm cần lưu ý khi code

- Export từ Stitch là HTML tĩnh, chưa có routing, state hay dữ liệu thật; các chỗ như ngày chuyến, mã vé `WB-01-A2`, giá 15.000 VND đang là dữ liệu mẫu.
- Nên chuyển Tailwind CDN sang cấu hình build (dùng token trong `DESIGN.md`) ngay ở `feature/foundation`.
- Một số màn hình không có thẻ `<title>`, cần bổ sung khi tách thành trang.
