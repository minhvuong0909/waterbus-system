# 📱 Smart Waterbus Staff Scanner (myApp)

Ứng dụng quét và kiểm soát vé điện tử tại bến tàu thủy Smart Waterbus dành cho Nhân viên soát vé (**Staff Scanner**).

- **Công nghệ**: [Expo SDK 57](https://expo.dev/) (`57.0.30`), [React Native 0.86](https://reactnative.dev/), [Expo Router 57](https://docs.expo.dev/router/introduction/), [SQLite Local Storage](https://docs.expo.dev/versions/latest/sdk/sqlite/).
- **Nguyên tắc cốt lõi**: Xác thực chữ ký số **Offline 100%**, cơ chế khử trùng lặp giao dịch **Idempotent Batch Sync**, nạp trước danh bạ vé vào SQLite.

---

## ⚡ Bắt Đầu Nhanh (Getting Started)

### 1. Cài đặt các gói phụ thuộc
```bash
pnpm install
```

### 2. Khởi chạy môi trường phát triển
```bash
pnpm dev
```
> Hoặc chạy: `pnpm start`

- Quét mã QR bằng ứng dụng **Expo Go (SDK 57)** trên điện thoại iOS / Android.
- Nhấn `w` để mở xem thử trên Web.
- Nhấn `a` để mở trên Android Emulator.
- Nhấn `r` để nạp lại bundle sau khi cập nhật mã nguồn.

---

## 🧪 Kiểm Thử Nghiệp Vụ Tự Động

Chạy bộ kiểm thử tự động 13 kịch bản offline QR, SQLite cache và sync worker:

```bash
pnpm run test:scanner
```

Kết quả: **13/13 tests passed (100%)**.

---

## 📂 Các Màn Hình Chính Trong `app/`

- **`(tabs)/index.tsx`**: Tổng quan trạng thái ca trực, số lượng vé đã quét, kết nối đồng bộ.
- **`(tabs)/scanner.tsx`**: Camera quét QR tốc độ cao, hỗ trợ đèn flash và danh sách tích chọn hành khách trong Booking.
- **`(tabs)/manifest.tsx`**: Danh bạ hành khách và danh sách vé được nạp sẵn vào SQLite cho từng chuyến tàu.
- **`(tabs)/settings.tsx`**: Trạng thái hàng đợi đồng bộ ngoại tuyến và cập nhật Public Key.
