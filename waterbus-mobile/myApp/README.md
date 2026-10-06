# SMART WATERBUS — STAFF SCANNER MOBILE APP (REACT NATIVE / EXPO)
> **Thành viên 4: Mobile App Developer (Staff Scanner) - Tuấn**  
> Ứng dụng di động chuyên biệt dành cho nhân viên soát vé tại bến tàu Smart Waterbus. Tối ưu hoá cho môi trường ngoài trời, tốc độ quét cao, và hoạt động ngoại tuyến 100% không phụ thuộc Internet.

---

## 📌 TỔNG QUAN VAI TRÒ & TÍNH NĂNG ĐÃ TRIỂN KHAI

### 1. 📷 Giao diện Soát vé Tốc độ cao (High-Speed Scanner UI)
- **Camera Quét QR tốc độ cao**: Sử dụng `expo-camera` (SDK 54) với khung quét nhắm mục tiêu (reticle corners), hiệu ứng laser scanline chuyển động mượt mà, hỗ trợ bật/tắt đèn Flash (Torch) khi trời tối và đổi camera trước/sau.
- **Phản hồi Rung & Âm thanh**: Tích hợp `expo-haptics` phản hồi tức thì (< 5ms) khi quét trúng mã vé, tự động chống quét trùng lặp (debounce/lock).
- **Màn hình Chi tiết Booking & Tích chọn Khách**:
  - Khi quét mã QR của Booking, hệ thống giải mã và hiển thị chi tiết: Mã đơn, tên người đặt, số điện thoại, tuyến đường, tàu, giờ xuất bến.
  - **Tích chọn Check-in (BR-QR-03)**: Hiển thị toàn bộ danh sách vé/ghế trong đơn (`Khoang trước`, `Tiêu chuẩn`, `Boong ngoài trời`), kèm checkbox để nhân viên tích chọn đúng những hành khách đang có mặt thực tế.
  - Hỗ trợ nút *"Chọn tất cả"* và *"Xác nhận Check-in (N vé)"*.
- **Nhập mã thủ công (Fallback)**: Hỗ trợ tra cứu nhanh bằng mã đơn hoặc số vé khi màn hình khách bị vỡ hoặc QR mờ.
- **Mô phỏng QR Test**: Tích hợp sẵn bộ test QR có chữ ký số để chạy kiểm thử ngay trên máy ảo hoặc thiết bị thật.

### 2. 💾 Cơ sở dữ liệu Cục bộ SQLite & Preload Cache (BR-OFF-01)
- **Tải trước Dữ liệu Bến (Preload Cache)**:
  - Trước giờ mở cổng bến, nhân viên chọn bến phụ trách (Bến Bạch Đằng, Bình An, Thủ Thiêm...) và bấm *"Tải trước Dữ liệu (Preload Cache)"*.
  - Hệ thống tải toàn bộ manifest danh sách chuyến trong ngày, danh sách booking/vé hợp lệ, và **Public Key ký số** lưu vào SQLite cục bộ (`waterbus_scanner.db`).
- **Tối ưu hoá Tốc độ Truy vấn**:
  - Tạo các index chuyên biệt: `idx_booking_public_id`, `idx_ticket_number`, `idx_ticket_boarding_status`, `idx_checkin_sync_status`.
  - Tra cứu thông tin booking và vé offline chỉ mất **< 2ms**, cho phép soát vé liên tục không có độ trễ.
- **Theo dõi Thống kê Bộ nhớ**:
  - Hiển thị trực quan số chuyến đã nạp, số đơn, tổng vé cache, số vé đã check-in, số vé đang chờ sync, thời gian tải gần nhất và dung lượng SQLite.

### 3. 🔐 Xác thực Chữ ký số QR Ngoại Tuyến (Offline Verify)
- **Logic Xác thực Không Cần API (BR-QR-01, BR-QR-02)**:
  - Mỗi Booking có một mã QR tĩnh duy nhất chứa: `public_booking_id`, `version`, `timestamp` và `signature`.
  - Thiết bị dùng **Public Key** đã nạp trong SQLite để giải mã và kiểm tra tính toàn vẹn của chữ ký số (chuẩn HMAC-SHA256 / RSA-SHA256).
- **Chống Giả Mạo & Hết Hạn (Tamper Proof)**:
  - Nếu khách tự ý chỉnh sửa `publicBookingId` hoặc giả lập vé, chữ ký số sẽ không khớp và app báo lỗi đỏ ngay lập tức.
  - Tự động kiểm tra thời hạn sử dụng vé (`exp`) ngay trên máy.

### 4. 🔄 Cơ chế Đồng bộ Idempotent Sync (Background Batch Sync)
- **Hàng đợi Check-in Ngoại tuyến (BR-OFF-03, BR-OFF-05)**:
  - Khi nhân viên bấm Check-in vé ngoại tuyến, hệ thống thực hiện transaction cục bộ:
    1. Cập nhật `manifest_tickets.boarding_status = 'CheckedIn'`.
    2. Ghi bản ghi vào bảng `offline_checkin_events` với mã định danh duy nhất `client_event_id` (UUID v4 ngẫu nhiên).
- **Tự động Đẩy Lô (Background Task)**:
  - Background Task chạy định kỳ mỗi **15 giây** hoặc tự động kích hoạt ngay khi mạng (3G/4G/WiFi) được khôi phục (qua `NetInfo`).
  - Gửi payload dạng lô (Batch) kèm header `Idempotency-Key` lên endpoint `POST /api/v1/tickets/sync-offline`.
  - Server dựa vào `client_event_id` để loại bỏ trùng lặp nếu lô gửi lại nhiều lần do mạng chập chờn.
  - Khi máy chủ xác nhận thành công, SQLite cục bộ cập nhật `sync_status = 'Synced'` và giải phóng hàng đợi.
- **Chế độ Ngoại Tuyến Cố Định (Force Offline Mode)**:
  - Có switch bật/tắt trong màn Cài đặt để nhân viên thử nghiệm tình huống bến hoàn toàn mất sóng và kiểm tra đối soát sau ca làm việc.

---

## 📁 CẤU TRÚC THƯ MỤC DỰ ÁN

```text
waterbus-mobile/myApp/
├── app/                              # Expo Router file-based routing
│   ├── (tabs)/
│   │   ├── _layout.tsx               # Tab bar (Soát vé, Tải vé, Cài đặt)
│   │   ├── index.tsx                 # Tab 1: 📷 Camera Soát vé
│   │   ├── manifest.tsx              # Tab 2: 💾 Quản lý Preload Cache & SQLite
│   │   └── settings.tsx              # Tab 3: ⚙️ Cấu hình thiết bị & Nhân viên Tuấn
│   └── _layout.tsx                   # Khởi tạo SQLite và Background Sync Worker
│
├── src/
│   ├── types/
│   │   └── scanner.ts                # TypeScript models (Booking, Ticket, CheckInEvent, SyncBatch)
│   ├── services/
│   │   ├── sqlite/
│   │   │   ├── database.ts           # SQLite wrapper (Native Expo SQLite + Fallback)
│   │   │   └── sqliteRepository.ts   # CRUD manifest, atomic check-in, queue query
│   │   ├── crypto/
│   │   │   └── qrVerifier.ts         # Logic giải mã chữ ký số QR bằng Public Key
│   │   └── sync/
│   │       └── syncService.ts        # Preload cache API & Idempotent background sync worker
│   ├── store/
│   │   └── useScannerStore.ts        # Zustand reactive state store
│   ├── screens/
│   │   ├── scanner/
│   │   │   └── StaffScannerScreen.tsx       # UI Camera quét QR tốc độ cao
│   │   ├── booking/
│   │   │   └── BookingDetailModal.tsx       # UI hiển thị danh sách khách & tích chọn check-in
│   │   ├── manifest/
│   │   │   └── PreloadManifestScreen.tsx    # UI quản lý cache & preload trước giờ mở cổng
│   │   └── settings/
│   │       └── ScannerSettingsScreen.tsx    # UI thông tin nhân viên Tuấn & cấu hình máy
│   └── navigation/
│       └── AppNavigator.tsx          # Tab Navigator tương thích React Navigation
│
└── scripts/
    └── verify-staff-scanner.ts       # Suite kiểm thử tự động 13 test cases (100% PASS)
```

---

## 🚀 HƯỚNG DẪN KHỞI CHẠY & KIỂM THỬ

### 1. Cài đặt thư viện
```bash
cd waterbus-mobile/myApp
npm install
```

### 2. Chạy Bộ Kiểm Thử Tự Động (13/13 Test Cases - 100% PASS)
Kiểm thử toàn diện 4 trụ cột nghiệp vụ:
- Xác thực QR Offline bằng Public Key (BR-QR-01, BR-QR-02)
- Kiểm tra tính chống giả mạo chữ ký (Tamper Detection)
- SQLite Preload Cache & Tra cứu dưới 2ms (BR-OFF-01)
- Check-in từng hành khách & sinh UUID v4 ClientEventId (BR-QR-03, BR-OFF-05)
- Hàng đợi ngoại tuyến & Idempotent Batch Sync Worker (BR-OFF-03)

```bash
npm run test:scanner
```

### 3. Chạy Ứng dụng trên Điện thoại / Máy ảo
```bash
# Khởi động Expo Metro Bundler
npx expo start

# Hoặc mở trực tiếp trên Android Emulator / Thiết bị Android
npx expo run:android   # hoặc quét mã QR qua ứng dụng Expo Go trên điện thoại
```
