# 🌐 Waterbus Web Frontend (React + Vite + TypeScript)

Cổng thông tin giao diện Web dành cho Khách hàng (Passenger / Guest), Ban Quản Trị (Admin) và Thuyền trưởng (Captain) của hệ thống **Smart Waterbus**. Xây dựng với **React 18**, **TypeScript**, **Vite** và **TailwindCSS**, kết nối API thời gian thực và đồng bộ ghế qua SignalR.

---

## 🌟 Phân Hệ & Tính Năng Trọng Tâm

### 1️⃣ Khách Hàng & Vãng Lai (Customer / Guest)
- **Tra cứu chuyến & Lịch trình**: Tìm chuyến theo bến xuất phát, bến đến, ngày giờ đi và số lượng hành khách.
- **Sơ đồ chọn ghế trực quan (Visual Seat Map)**: Phân bố ghế theo tầng, hiển thị tình trạng ghế trống/đang giữ/đã bán theo từng chặng dừng.
- **Đồng bộ ghế thời gian thực (SignalR Seat Sync)**: Cập nhật tức thời ghế đang được khách khác giữ tạm hoặc đã thanh toán mà không cần tải lại trang.
- **Đặt chỗ & Thanh toán**: Tích hợp cổng thanh toán VNPay, tự động kiểm tra thời hạn giữ chỗ (Hold TTL).
- **Ví vé điện tử (Ticket Wallet)**: Quản lý vé điện tử, mã QR check-in bảo mật và gửi yêu cầu đổi/hủy vé trực tuyến.

### 2️⃣ Quản Trị & Vận Hành (Admin & Captain Portal)
- **Radar Giám sát vị trí tàu (Live GPS Fleet Tracking)**: Bản đồ Leaflet thể hiện hành trình trực tiếp của các tàu buýt trên sông Sài Gòn.
- **Quản lý sự cố & Thời tiết (Incident Management)**: Tiếp nhận cảnh báo giông bão từ Weather Guard, thuyền trưởng gửi tọa độ GPS và thông báo an toàn.

### 3️⃣ Trải Nghiệm Du Thuyền (Sightseeing & AI Audio)
- Khám phá các tour du ngoạn ngắm hoàng hôn, cầu Ba Son, bến Nhà Rồng.
- Trình phát âm thanh thuyết minh địa danh (Dark-tone Audio Player) đồng bộ theo tọa độ di chuyển.

---

## 📁 Cấu Trúc Thư Mục (Project Structure)

Tuân thủ nghiêm ngặt quy ước thư mục của dự án (`AGENTS.md`):

```text
waterbus-fe/
├── public/                 # Tài nguyên tĩnh (Favicon, logo bến tàu, âm thanh...)
├── src/
│   ├── assets/             # Hình ảnh bến tàu, vector icon, biểu trưng
│   ├── components/         # Các thành phần UI dùng chung (Header, Footer, Button, Modal, Loader)
│   ├── constant/           # Hằng số toàn cục, cấu hình theme, apiEndpoints.ts
│   ├── features/           # Các lát cắt nghiệp vụ (Chứa UI, Hooks, Services riêng)
│   │   ├── authentication/ # Đăng nhập, đăng ký tài khoản, phân quyền
│   │   ├── booking/        # Sơ đồ chọn ghế, thông tin hành khách, giữ chỗ
│   │   ├── explore/        # Khám phá tour du lịch sông Sài Gòn
│   │   ├── home/           # Trang chủ, bản đồ tổng quan các bến tàu
│   │   ├── payment/        # Cổng VNPay, mã QR, xác nhận đơn hàng
│   │   ├── tickets/        # Ví vé điện tử, QR check-in, lịch sử giao dịch
│   │   ├── tracking/       # Bản đồ trực tiếp tàu di chuyển (Leaflet + SignalR)
│   │   └── trips/          # Tìm kiếm danh sách chuyến tàu & thông tin chi tiết
│   ├── hooks/              # Custom React hooks (useFetch, useDebounce, useSignalR...)
│   ├── i18n/               # Hỗ trợ đa ngôn ngữ (Tiếng Việt & English)
│   ├── layouts/            # Khung giao diện chính (MainLayout, AuthLayout, AdminLayout)
│   ├── routes/             # Cấu hình định tuyến (AppRoutes, Public & Protected Routes)
│   ├── store/              # Quản lý state toàn cục (Redux Toolkit / Context)
│   ├── utils/              # Tiện ích định dạng tiền tệ (vi-VN), ngày giờ UTC -> VN
│   ├── App.tsx             # Root Component
│   ├── index.css           # Cấu hình TailwindCSS & Global CSS
│   └── main.tsx            # Entry point
├── .env.example            # Biến môi trường mẫu
├── package.json            # Thư viện phụ thuộc & Scripts
├── tsconfig.json           # Cấu hình TypeScript
└── vite.config.ts          # Cấu hình Vite bundler
```

---

## ⚙️ Cấu Hình Môi Trường (.env)

Sao chép `.env.example` thành `.env`:

| Biến môi trường | Giá trị mặc định | Giải thích |
|---|---|---|
| `VITE_API_BASE_URL` | `/api/v1` (hoặc `http://localhost:5000`) | Đường dẫn gốc API Backend |
| `VITE_SIGNALR_URL` | `/hubs` (hoặc `http://localhost:5000/hubs`) | Endpoint kết nối SignalR Hubs |
| `VITE_USE_MOCK` | `false` | `true`: dùng mock data cho dev độc lập; `false`: kết nối BE thật |

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### Yêu cầu:
- **Node.js**: >= 18.x
- **Package Manager**: `pnpm` (hoặc `npm`)

### Các lệnh thực thi:

```bash
# 1. Cài đặt dependencies
pnpm install

# 2. Khởi chạy môi trường phát triển (Development)
pnpm dev
# Truy cập giao diện tại: http://localhost:5173

# 3. Kiểm tra kiểu dữ liệu TypeScript & Linter
pnpm typecheck
pnpm lint

# 4. Build sản phẩm hoàn chỉnh (Production)
pnpm build
```

---

## 🔒 Quy Ước Bắt Buộc Khi Lập Trình Frontend

- **Mọi yêu cầu API**: Bắt buộc khai báo tại `src/constant/apiEndpoints.ts` và bọc trong service chuyên biệt thuộc `src/features/<tên>/services`. Không gọi `fetch` rải rác trong component.
- **SignalR Service**: Bọc kết nối SignalR trong một service tập trung (có cơ chế auto-reconnect và tự động join lại group sau khi rớt mạng).
- **Xử lý thời gian & Tiền tệ**: Thời gian nhận từ API luôn là UTC (`DateTimeOffset`), bắt buộc hiển thị theo giờ Việt Nam (GMT+7). Tiền tệ luôn định dạng chuẩn `vi-VN` (ví dụ: `15.000 ₫`).
- **Phân quyền 5 Actor**: `Passenger`, `Guest`, `Admin`, `Staff`, `Captain` (*Tuyệt đối không có actor Dispatcher theo chuẩn BR v2.0*).
