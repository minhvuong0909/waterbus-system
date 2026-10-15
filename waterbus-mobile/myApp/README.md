# Welcome to your Expo app 👋

This is an [Expo](https://expo.dev) project created with [`create-expo-app`](https://www.npmjs.com/package/create-expo-app).

## Get started

1. Install dependencies

   ```bash
   npm install
   ```

2. Start the app

   ```bash
   npx expo start
   ```

In the output, you'll find options to open the app in a

- [development build](https://docs.expo.dev/develop/development-builds/introduction/)
- [Android emulator](https://docs.expo.dev/workflow/android-studio-emulator/)
- [iOS simulator](https://docs.expo.dev/workflow/ios-simulator/)
- [Expo Go](https://expo.dev/go), a limited sandbox for trying out app development with Expo

You can start developing by editing the files inside the **app** directory. This project uses [file-based routing](https://docs.expo.dev/router/introduction).

## Get a fresh project

When you're ready, run:

```bash
npm run reset-project
```

This command will move the starter code to the **app-example** directory and create a blank **app** directory where you can start developing.

## Learn more

To learn more about developing your project with Expo, look at the following resources:

- [Expo documentation](https://docs.expo.dev/): Learn fundamentals, or go into advanced topics with our [guides](https://docs.expo.dev/guides).
- [Learn Expo tutorial](https://docs.expo.dev/tutorial/introduction/): Follow a step-by-step tutorial where you'll create a project that runs on Android, iOS, and the web.

## Join the community

Join our community of developers creating universal apps.

- [Expo on GitHub](https://github.com/expo/expo): View our open source platform and contribute.
- [Discord community](https://chat.expo.dev): Chat with Expo users and ask questions.

---

## 📱 Module Soát Vé (Thành viên 4: Mobile App Developer - Tuấn)

Module dành cho nhân viên soát vé (Staff Scanner) tại bến tàu Smart Waterbus.

### Chức năng chính:
1. **Giao diện Soát vé (Camera QR Scanner)**: Quét mã QR tốc độ cao, hỗ trợ flash, laser scanline, phản hồi rung haptic, hiển thị danh sách vé trong Booking và tích chọn hành khách check-in (BR-QR-03).
2. **Cơ sở dữ liệu cục bộ (SQLite)**: Preload cache danh sách vé hợp lệ, thông tin chuyến và Public Key vào máy trước giờ mở cổng bến (BR-OFF-01).
3. **Verify QR Offline**: Giải mã và xác thực chữ ký số bằng Public Key trực tiếp trên máy không cần gọi API (BR-QR-01, BR-QR-02).
4. **Cơ chế Đồng bộ Idempotent Sync**: Background Task lưu sự kiện ngoại tuyến (`client_event_id` UUID v4 duy nhất) và tự động đẩy lô (Batch sync) lên Server khi có mạng (BR-OFF-03).

### Chạy kiểm thử tự động:
```bash
npm run test:scanner
```
