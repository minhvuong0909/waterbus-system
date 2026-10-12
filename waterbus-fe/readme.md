# 🚤 Smart Waterbus - Hệ Thống Đặt Vé & Theo Dõi Tàu Thủy Sông Sài Gòn

![React](https://img.shields.io/badge/React-18.2-blue?logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-4.9-blue?logo=typescript)
![Vite](https://img.shields.io/badge/Vite-4.2-purple?logo=vite)
![TailwindCSS](https://img.shields.io/badge/TailwindCSS-3.4-38bdf8?logo=tailwindcss)
![i18n](https://img.shields.io/badge/i18n-VI%20%7C%20EN-green)

**Smart Waterbus** là nền tảng ứng dụng web hiện đại phục vụ việc đặt vé, chọn ghế, quản lý vé điện tử và theo dõi hành trình tàu thủy trực tiếp theo thời gian thực (Live GPS Tracking) cho giao thông đường thủy đô thị.

---

## 🌟 Tính Năng Nổi Bật (Key Features)

### 1️⃣ Tìm Kiếm & Đặt Vé (Trip Search & Booking Flow)
- **Tìm kiếm chuyến tàu**: Chọn bến đi/bến đến (Bạch Đằng, Bình An, Thanh Đa, Linh Đông...), ngày đi và số lượng hành khách.
- **Sơ đồ chọn ghế trực quan (Seat Map)**: Hỗ trợ chọn ghế tầng trệt, boong tàu ngắm cảnh và phân loại hạng ghế.
- **Điền thông tin & Ưu đãi**: Quản lý thông tin hành khách, áp dụng mã giảm giá (Voucher) tự động tính toán lại mức giảm.
- **Xác nhận đơn hàng**: Xem lại tóm tắt hành trình và quy định vận chuyển trước khi thanh toán.

### 2️⃣ Thanh Toán & Vé Điện Tử (Payment & E-Tickets)
- **Cổng thanh toán đa dạng**: Hỗ trợ quét mã QR chuyển khoản, MoMo, ZaloPay và thẻ ngân hàng.
- **Mã QR Động & Chức năng sao chép**: Tiện lợi khi chuyển khoản trực tiếp với mã giao dịch tự động.
- **Ví vé điện tử (Ticket Wallet)**: Lưu trữ vé đã đặt, vé đã sử dụng và vé đã hủy.
- **Mã QR Check-in**: Xuất vé điện tử chứa mã QR để soát vé nhanh tại bến tàu.
- **Quản lý & Hủy vé (Manage Booking)**: Đổi lịch chuyến đi hoặc gửi yêu cầu hoàn vé trực tuyến.

### 3️⃣ Theo Dõi Hành Trình Realtime (Live Trip Tracking)
- **Bản đồ trực tuyến (Live GPS Map)**: Theo dõi vị trí tàu di chuyển thực tế trên dòng sông.
- **Thuyết minh du lịch (POI Audio Player)**: Tự động phát âm thanh thuyết minh các danh lam thắng cảnh dọc sông Sài Gòn.
- **Lịch trình & Thời gian dự kiến (ETA)**: Cập nhật chi tiết các bến dừng tiếp theo.

### 4️⃣ Khám Phá & Trải Nghiệm (Explore River Journeys)
- Tổng hợp các tuyến du ngoạn ngắm hoàng hôn, ngắm thành phố về đêm.
- Giao diện phát âm thanh tối (Dark-tone Audio Player) chuyên nghiệp.

### 5️⃣ Đa Ngôn Ngữ & Trải Nghiệm Người Dùng (i18n & UX)
- **Hỗ trợ Tiếng Việt (VI) & Tiếng Anh (EN)** với bộ từ điển hơn 1.000+ cụm từ được lazy-load riêng biệt.
- Tự động định dạng số, tiền tệ (VND) và ngày tháng chuẩn hóa theo ngôn ngữ lựa chọn.
- **Code-Splitting & Lazy Loading**: Tối ưu tốc độ tải trang (dung lượng main bundle giảm 50%+).
- **Giả lập lỗi & Thử lại (Error State & Retry)**: Tự động xử lý khi mất kết nối hoặc dữ liệu tải thất bại, hỗ trợ URL query `?mockError=N` cho demo/testing.

---

## 📁 Cấu Trúc Thư Mục Dự Án (Project Structure)

```text
smart-waterbus/
├── public/                 # Tài nguyên tĩnh (Favicon, hình ảnh tĩnh, audio...)
├── src/
│   ├── assets/             # Hình ảnh tàu, icon, logo, fonts
│   ├── components/         # Component UI tái sử dụng (Header, Footer, Button, PageLoader, ErrorState...)
│   ├── constant/           # Hằng số ứng dụng, danh sách bến tàu, loại vé
│   ├── features/           # Các mô-đun nghiệp vụ chính
│   │   ├── authentication/ # Đăng nhập, đăng ký, tra cứu khách bến
│   │   ├── booking/        # Chọn ghế, thông tin hành khách, voucher
│   │   ├── explore/        # Khám phá tour & âm thanh thuyết minh
│   │   ├── home/           # Trang chủ, sơ đồ hành lang sông
│   │   ├── payment/        # Phương thức thanh toán, QR code, xác nhận vé
│   │   ├── tickets/        # Ví vé, chi tiết vé, hủy vé & đổi lịch
│   │   ├── tracking/       # Bản đồ tracking live & thuyết minh POI
│   │   └── trips/          # Tìm kiếm chuyến & chi tiết chuyến đi
│   ├── hooks/              # Custom React Hooks (useFetch, useDebounce...)
│   ├── i18n/               # Đa ngôn ngữ (I18nProvider, translate, từ điển VI/EN)
│   ├── layouts/            # Layout chính của trang (MainLayout, AuthLayout)
│   ├── mocks/              # Dữ liệu giả lập (Mock Services & Data)
│   ├── pages/              # Các trang theo tuyến đường (Route Pages)
│   ├── routes/             # Cấu hình định tuyến (AppRoutes, Sitemaps, Private/Public Routes)
│   ├── store/              # Quản lý state toàn cục
│   ├── utils/              # Tiện ích định dạng tiền tệ, ngày tháng, helper
│   ├── App.tsx             # Root Component
│   ├── index.css           # Cấu hình TailwindCSS & Global CSS
│   └── main.tsx            # Entry point
├── index.html              # HTML template
├── package.json            # Dependencies & Scripts
├── tsconfig.json           # Cấu hình TypeScript
└── vite.config.ts          # Cấu hình Vite bundler
```

---

## 🗺️ Danh Sách Route Trong Ứng Dụng (Routes Map)

| Route | Đường dẫn | Mô tả |
| :--- | :--- | :--- |
| **Trang chủ** | `/` | Tra cứu lịch trình, bản đồ tuyến sông |
| **Đăng nhập** | `/sign-in` | Đăng nhập & tra cứu khách vãng lai |
| **Tìm chuyến** | `/search` \| `/search/results` | Tìm kiếm chuyến tàu & danh sách kết quả |
| **Chi tiết chuyến** | `/trip` | Thông tin tàu, bến đến & dịch vụ |
| **Đặt vé** | `/booking/seats` | Sơ đồ chọn ghế |
| **Hành khách** | `/booking/passenger` | Điền thông tin & mã giảm giá |
| **Rà soát** | `/booking/review` | Kiểm tra lại đơn hàng |
| **Thanh toán** | `/payment` | Quét mã QR / Chọn phương thức thanh toán |
| **Xác nhận** | `/payment/success` | Vé điện tử đã được xác nhận |
| **Ví vé** | `/tickets` | Quản lý danh sách vé đã đặt |
| **Chi tiết vé** | `/tickets/detail` | Mã QR check-in tại bến |
| **Đổi/Hủy vé** | `/tickets/manage` | Quản lý đổi chuyến & hoàn tiền |
| **Live Tracking** | `/tracking` | Bản đồ GPS thời gian thực & Audio Guide |
| **Khám phá** | `/explore` | Du ngoạn sông Sài Gòn & Thuyết minh |
| **Sitemap** | `/sitemap` | Sơ đồ nhanh tất cả các trang |

---

## 📦 Công Nghệ Sử Dụng (Tech Stack)

- **Core Framework**: [React 18](https://react.dev/) + [TypeScript 4.9](https://www.typescriptlang.org/)
- **Build Tool**: [Vite 4](https://vitejs.dev/)
- **Styling**: [TailwindCSS 3](https://tailwindcss.com/) + Material Symbols Icons
- **Routing**: [React Router v6](https://reactrouter.com/) (Route-level Code Splitting)
- **State & Data**: React Context + Custom Hooks + Localized Mock Services

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Dự Án (Getting Started)

### Yêu cầu môi trường
- **Node.js**: >= 18.x
- **npm**: >= 9.x

### 1️⃣ Cài đặt dependencies

```bash
npm install
```

### 2️⃣ Chạy ở môi trường phát triển (Development)

```bash
npm run dev
```
Mở trình duyệt tại đường dẫn mặc định: `http://localhost:5173`

### 3️⃣ Kiểm tra lỗi TypeScript & Linting

```bash
# Kiểm tra Type Safety
npm run typecheck

# Kiểm tra Linter
npm run lint
```

### 4️⃣ Build sản phẩm (Production Build)

```bash
npm run build
```

Xem trước bản build đã nén:
```bash
npm run preview
```

---

## 👨‍💻 Tác Giả & Đóng Góp (Author)

- **Repository**: [dinh-vien/smart-waterbus](https://github.com/dinh-vien/smart-waterbus)
- **Email**: vienaone1475@gmail.com
