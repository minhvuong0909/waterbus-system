# 📱 Waterbus Mobile (Staff Scanner App)

Ứng dụng di động chuyên dụng dành riêng cho **Nhân viên soát vé tại bến tàu (Staff Scanner)** của hệ thống Smart Waterbus. Ứng dụng được xây dựng trên nền tảng **React Native**, **Expo SDK 57 (`57.0.30`)**, **Expo Router** và **SQLite nội bộ** để thực hiện soát vé ngoại tuyến tốc độ cao và đồng bộ dữ liệu bảo mật.

---

## 🎯 Mục Tiêu & Ranh Giới Nghiệp Vụ

Theo quy định kiến trúc trong [AGENTS.md](../AGENTS.md):
- **Phạm vi duy nhất**: Phục vụ **Staff Scanner** tại bến tàu. Không xây dựng app hành khách hay app thuyền trưởng trên nền tảng mobile này.
- **Xác thực QR ngoại tuyến (Offline Verification)**: Giải mã và kiểm tra chữ ký số bằng Public Key được lưu trong máy, **hoàn toàn không gọi API mỗi lượt quét** để đảm bảo tốc độ soát vé tại cổng dưới 100ms.
- **Cơ chế Khử Trùng Lặp (Idempotent Batch Sync)**: Mỗi lượt check-in được gán mã `clientEventId` duy nhất (UUID v4). Khi có kết nối mạng, các sự kiện ngoại tuyến được đẩy theo lô lên backend (Server dựa vào `clientEventId` để khử trùng lặp tuyệt đối).
- **Cửa sổ Soát Vé**: Chỉ cho phép check-in trong khoảng thời gian hợp lệ `CheckInOpenAt` – `CheckInCloseAt` của đúng chặng dừng `TripStopCall` được phân công.

---

## 🏗️ Cấu Trúc Thư Mục Ứng Dụng

Mã nguồn ứng dụng nằm tại thư mục [`myApp/`](myApp/):

```text
waterbus-mobile/myApp/
├── app/                        # Expo Router (File-based navigation)
│   ├── (tabs)/                 # Tab Layout chính
│   │   ├── index.html/index.tsx# Màn hình Tổng quan ca trực & Trạng thái bến
│   │   ├── scanner.tsx         # Màn hình Quét Camera QR code
│   │   ├── manifest.tsx        # Danh sách vé & Hành khách chuyến tàu
│   │   ├── settings.tsx        # Cấu hình thiết bị & Trạng thái Sync
│   │   └── _layout.tsx         # Bottom Tab navigation & Haptic feedback
│   └── _layout.tsx             # Root Layout (SQLite init & Sync Worker lifecycle)
│
├── src/
│   ├── screens/                # Giao diện nghiệp vụ chi tiết
│   │   ├── scanner/            # StaffScannerScreen (Camera, laser guide, checklist)
│   │   ├── manifest/           # PreloadManifestScreen (Bộ nhớ đệm chuyến & vé)
│   │   └── settings/           # ScannerSettingsScreen (Đồng bộ, Public Key)
│   ├── services/
│   │   ├── qr/                 # QrVerifierService (Giải mã chữ ký số RSA/HMAC offline)
│   │   ├── sqlite/             # SQLite Database & Repositories lưu trữ offline
│   │   └── sync/               # SyncService (Idempotent background batch sync worker)
│   └── store/                  # Quản lý trạng thái thiết bị (Zustand)
│
├── scripts/
│   └── verify-staff-scanner.ts # Bộ kiểm thử tự động 13 kịch bản nghiệp vụ soát vé
├── package.json                # Expo SDK 57 & Dependencies tương thích
└── app.json                    # Cấu hình Expo, quyền Camera, SQLite plugins
```

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### 1. Yêu cầu môi trường
- **Node.js**: >= 18.x
- **pnpm**: >= 9.x
- **Ứng dụng Expo Go**: Phiên bản hỗ trợ SDK 57 trên thiết bị iOS (App Store) hoặc Android (Google Play).

### 2. Khởi chạy Ứng dụng
```bash
cd waterbus-mobile/myApp

# Cài đặt thư viện
pnpm install

# Khởi chạy Metro Bundler
pnpm dev
# (hoặc pnpm start)
```
- Mở ứng dụng **Expo Go** trên điện thoại và quét mã QR hiển thị trong terminal để tải app.
- Nhấn phím `w` để chạy thử nghiệm trên trình duyệt Web.
- Nhấn phím `a` để mở máy ảo Android Emulator.

---

## 🧪 Kiểm Thử Tự Động (Verification Suite)

Ứng dụng đi kèm bộ test tự động kiểm thử toàn diện 4 nhóm quy tắc nghiệp vụ:

```bash
cd waterbus-mobile/myApp
pnpm run test:scanner
```

### 4 Nhóm Kịch Bản Được Kiểm Chứng (13/13 Pass):
1. **Offline QR Verification (`BR-QR-01`, `BR-QR-02`)**: Xác thực chữ ký số offline, chặn vé giả mạo, chặn vé hết hạn.
2. **SQLite Preload Cache (`BR-OFF-01`)**: Nạp trước danh bạ chuyến và vé hợp lệ trước giờ mở cổng, tra cứu siêu tốc 0ms.
3. **Passenger Selection & Check-In (`BR-QR-03`, `BR-OFF-05`)**: Tích chọn hành khách có mặt, sinh `clientEventId` UUID v4 duy nhất.
4. **Idempotent Batch Sync Worker (`BR-OFF-03`)**: Hàng đợi sự kiện ngoại tuyến và cơ chế đẩy lô an toàn lên máy chủ.
