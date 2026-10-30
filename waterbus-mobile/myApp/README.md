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
4. **Cơ chế Đồng bộ Idempotent Sync**: lưu sự kiện ngoại tuyến (`client_event_id` UUID v4 duy nhất) và gửi theo lô khi ứng dụng đang chạy, có mạng (BR-OFF-03).

### Quy tắc xác thực và đồng bộ

- QR chỉ được xác thực bằng chữ ký `RS256` với khóa công khai PEM lấy từ manifest đã tải về. Ứng dụng không dùng khóa bí mật mặc định, không chấp nhận `HS256`, mã đặt vé thuần hoặc chữ ký giả.
- Nếu không tải được manifest thật hoặc manifest thiếu khóa `RS256` hợp lệ, thao tác preload báo lỗi và không thay bằng dữ liệu mẫu.
- Một lượt soát chỉ chuyển sang `Synced` khi phản hồi máy chủ xác nhận đúng `clientEventId`, `syncStatus = Synced` và `outcome = Success`. Lỗi mạng, phản hồi thiếu hoặc không xác nhận từng sự kiện sẽ giữ sự kiện ở trạng thái chờ và ghi nhận lần thử lại.
- Khóa `Idempotency-Key` của lô được tạo ổn định từ mã thiết bị và các `clientEventId`; gửi lại cùng lô sẽ dùng lại khóa đó.
- Bộ gửi lặp chạy trong tiến trình ứng dụng; Android không đảm bảo nó tiếp tục chạy sau khi ứng dụng bị đóng hoặc hệ điều hành dừng ứng dụng.

### Chạy kiểm thử tự động:
```bash
npm run test:scanner
```

### Hướng dẫn kiểm thử thủ công trên điện thoại Android

Hướng dẫn này bắt đầu từ lúc app vừa mở trên điện thoại. Thực hiện trên Android; tên tab/nút bên dưới tương ứng với giao diện hiện tại.

> **An toàn khi test:** chỉ dùng booking được dành riêng cho kiểm thử trên backend test. Không check-in vé thật, không xóa cache khi còn sự kiện offline cần giữ, và không bật đồng bộ lên server sản xuất.

#### A. Mở app lần đầu và chuẩn bị dữ liệu

1. **Mở app.** Nếu app đang hiện màn hình Expo/Metro báo lỗi thay vì giao diện soát vé, dừng ở đây: khởi động lại dev server với cache mới (`npx expo start --clear --lan` trong thư mục `waterbus-mobile\myApp`), kết nối điện thoại và máy tính vào cùng Wi-Fi, rồi mở lại app.
2. **Cấp quyền camera.** Khi Android hỏi, chọn cho phép. Nếu trước đó đã từ chối, vào **Cài đặt Android → Ứng dụng → myApp → Quyền → Camera → Cho phép**, rồi quay lại app. Màn hình chính phải thấy hình camera và khung quét. Nếu chưa cấp quyền, bấm **Cấp Quyền Camera**.
3. **Nhận biết bốn tab ở cạnh dưới màn hình:**
   - **Soát Vé**: camera và nhập mã. Nhập mã thủ công chỉ tra cứu, không xác thực QR và không cho phép check-in.
   - **Chuyến & Tải Vé**: chọn bến, preload cache, xem chuyến và số liệu SQLite.
   - **Lịch Sử**: các sự kiện check-in trên máy và nút **Sync Lô**.
   - **Ca Trực**: hồ sơ thiết bị và công tắc **Mô Phỏng Ngoại Tuyến (Force Offline)**.
4. **Nạp dữ liệu kiểm thử:** chỉ tiếp tục nếu backend test có endpoint manifest và trả về dữ liệu booking/chuyến cùng khóa công khai `RS256` còn hoạt động. Mở **Chuyến & Tải Vé**, chọn bến được backend hỗ trợ, bấm **TẢI DỮ LIỆU VỀ THIẾT BỊ** và xác nhận thông báo tải thành công. Số chuyến/vé phụ thuộc dữ liệu backend.
5. Nếu endpoint không truy cập được, phản hồi lỗi hoặc thiếu khóa công khai hợp lệ, ứng dụng báo tải thất bại. Không có manifest mẫu tự tạo; không tiếp tục kiểm thử bằng dữ liệu cũ như thể đó là dữ liệu vừa tải.
6. **Không xóa cache để “làm sạch” một cách tùy tiện.** Nút xóa cache nằm cuối tab **Chuyến & Tải Vé**; thao tác này xóa cả dữ liệu SQLite và có thể làm mất sự kiện chưa đồng bộ. Chỉ dùng khi chủ động khởi tạo lại bài test và chắc chắn không cần giữ dữ liệu nào.

#### B. M13 — kiểm tra QR ký `RS256` ngoại tuyến

**Mục tiêu:** scanner xác minh chữ ký JWS `RS256` bằng khóa công khai đã tải, lấy `kid` từ header được bảo vệ và không gọi API trong lúc xác minh.

1. Hoàn thành phần A với manifest và booking test thật được cấp bởi backend.
2. Mở QR do backend phát hành cho booking đó trên một thiết bị khác, máy tính hoặc bản in. Không tự tạo/chỉnh sửa QR trên ứng dụng scanner.
3. Trên điện thoại scanner, mở tab **Soát Vé**. Hướng camera vào QR và giữ yên trong khung quét; nếu khó đọc, tăng độ sáng màn hình hiển thị QR hoặc bấm **Bật Đèn** khi quét bản in.
4. Chờ màn hình **Chi Tiết Booking & Soát Vé** xuất hiện. Kiểm tra:
   - Có banner **CHỮ KÝ SỐ HỢP LỆ (XÁC THỰC OFFLINE)**.
   - Thuật toán là `RS256`; `kid` khớp khóa công khai đã tải.
   - Booking nằm trong manifest đã preload.
   - Tắt mạng sau khi preload để xác nhận QR vẫn được xác minh offline.
5. Nhấn nút đóng ở góc trên để quay lại camera. **Không cần bấm XÁC NHẬN CHECK-IN** để kiểm tra M13.

**Kết quả đạt:** QR do backend phát hành mở đúng booking; banner chữ ký hợp lệ và `kid` khớp khóa đã tải.

**Nếu không đạt:** nếu camera không nhận QR, thử ảnh lớn/sáng hơn. Nếu báo chưa có dữ liệu, tải lại manifest đúng bến. Nếu chữ ký không hợp lệ hoặc khóa chưa được tải, không check-in booking đó.

Ứng dụng không còn tạo QR mẫu trên thiết bị: chỉ backend có khóa riêng mới được phát hành QR có thể xác thực. Mã đặt vé thuần và chữ ký `HS256` bị từ chối.

#### C. H03 — tạo sự kiện offline và kiểm tra đồng bộ trễ

H03 cần server quyết định từ chối sự kiện đến sau giờ đóng check-in. Với app/repository hiện tại, chỉ có thể kiểm tra phần **tạo và giữ sự kiện offline trên điện thoại**; chưa thể xác nhận phần server từ chối trễ.

##### C1. Kiểm tra ghi nhận offline an toàn trên điện thoại

1. Hoàn thành phần A, đảm bảo booking test đã được preload. Mở **Ca Trực**.
2. Bật **Mô Phỏng Ngoại Tuyến (Force Offline)**. Giữ công tắc bật trong suốt bài test; không đóng app giữa chừng vì cài đặt offline hiện chỉ giữ trong trạng thái chạy hiện tại.
3. Quay lại **Soát Vé**, quét QR `RS256` thật do backend phát hành cho booking test. Không dùng mã đặt vé nhập tay để mở luồng check-in.
4. Trong màn hình chi tiết, xác nhận đúng booking và có 3 vé chưa check-in. Chỉ chọn **một** vé thử, ví dụ ghế A01; không bấm **Chọn tất cả**.
5. Bấm **XÁC NHẬN CHECK-IN (1 VÉ)**. Chờ thông báo check-in thành công và đóng màn hình chi tiết.
6. Mở **Lịch Sử**. Sự kiện mới phải xuất hiện; ghi lại mã UUID ở dòng **Mã Event (UUID)**. Số **Chờ Sync** dự kiến tăng thêm 1. Có thể kiểm tra thêm tab **Chuyến & Tải Vé** ở chỉ số **Vé chờ đồng bộ (Offline Queue)**.
7. Đến đây chỉ kết luận: “check-in offline đã được lưu vào hàng đợi trên máy”. Giữ Force Offline bật và **không bấm `Sync Lô`** cho tới khi có môi trường backend kiểm thử đúng.

##### C2. Kiểm thử từ chối đồng bộ trễ qua backend (chỉ khi backend test đã sẵn sàng)

1. Xác nhận backend kiểm thử có endpoint xử lý đồng bộ offline, có chuyến/lượt bến thật, và có quy tắc đóng cửa sổ check-in. Đảm bảo cấu hình API trên điện thoại thực sự được service sử dụng; ô địa chỉ server trên màn hình **Ca Trực** hiện chưa được áp dụng vào service đồng bộ.
2. Dùng sự kiện test mới, chưa từng đồng bộ. Trước giờ đóng check-in, bật Force Offline, preload manifest từ backend và check-in đúng một vé test như phần C1.
3. Xác nhận sự kiện còn `Pending` trước giờ đóng; ghi lại UUID và thời điểm check-in. Để sự kiện trong máy đến **sau** giờ đóng được cấu hình ở backend.
4. Chỉ khi endpoint backend đã được xác nhận hoạt động và trỏ vào môi trường test, tắt Force Offline và bấm **Sync Lô** một lần. Lưu phản hồi/log server cho UUID đã ghi.
5. Kết quả server mong đợi: sự kiện bị từ chối vì cửa sổ check-in đã đóng. Kết quả lưu cục bộ mong đợi: `sync_status = Conflict`, `outcome = LateSyncRejected`, có lý do từ chối; không được ghi nhận là check-in thành công trên server.

Khi request lỗi, sự kiện được giữ `Pending`, tăng số lần thử và không được báo đã đồng bộ. Khi máy chủ chỉ xác nhận một phần, chỉ các `clientEventId` được xác nhận thành công mới chuyển `Synced`; sự kiện còn lại tiếp tục chờ gửi. Đối chiếu UUID và phản hồi backend khi nghiệm thu H03.

**Dừng lại nếu chưa có backend test:** repository hiện chưa có `/api/v1/tickets/sync-offline` xử lý sự kiện này. Không tắt Force Offline rồi bấm Sync để thử khi chưa có endpoint test; sự kiện sẽ tiếp tục chờ trên máy thay vì được đánh dấu đã đồng bộ.

#### D. M08 — hai thiết bị cùng giành Primary Scanner

**Không thể hoàn tất M08 chỉ bằng thao tác trên giao diện app hiện tại.** Tab **Ca Trực** hiển thị nhãn “Primary Offline Scanner” tĩnh; giao diện chưa có nút claim hoặc chọn `TripStopCall`, và backend repository chưa có `/api/v1/scanner/claim-primary`.

Khi phiên bản có luồng claim và backend test đã triển khai endpoint, thực hiện bài test tích hợp sau:

1. Chuẩn bị **hai điện thoại** cài app, hai mã thiết bị khác nhau, tài khoản/nhân viên test được phân quyền, cùng truy cập một backend kiểm thử.
2. Xác nhận thời gian hai máy gần nhau; chuẩn bị cùng một chuyến và đúng cùng `TripStopCall` còn hiệu lực. Không dùng mỗi `stationId` thay cho `TripStopCallId`.
3. Trên cả hai máy, mở ca trực/chuyến test tương ứng. Bắt đầu thao tác claim trên hai máy gần như đồng thời (hai người thao tác hoặc công cụ test API gửi song song).
4. Ghi lại phản hồi từng máy. Kết quả đạt là chỉ **một** thiết bị nhận `isPrimaryOffline = true`; thiết bị còn lại nhận conflict hoặc `isPrimaryOffline = false`.
5. Gửi claim lần nữa từ thiết bị thua cho cùng `TripStopCall`. Kết quả vẫn không được có hai Primary. Lưu request/response đã che thông tin nhạy cảm và log server để đối chiếu.

**Không kết luận M08 đạt chỉ vì một máy hiện nhãn Primary.** Trước khi endpoint và thao tác claim được nối vào app, chỉ có thể kiểm thử khi gọi API bằng công cụ kiểm thử riêng; đó không phải kiểm thử UI trên điện thoại.

#### E. Ghi nhận kết quả và dọn dẹp

Với mỗi mã lỗi, ghi lại ngày/giờ, mã bug (H03/M08/M13), model Android, phiên bản app, chế độ mạng, tên chuyến/lượt bến test, bước thực hiện, kết quả mong đợi/thực tế và ảnh chụp màn hình. Che tên/số điện thoại khách, token và dữ liệu cá nhân trước khi chia sẻ.

- **M13:** chụp banner chữ ký và dòng `kid`.
- **H03:** lưu UUID, thời điểm check-in, thời điểm gửi, phản hồi/log backend và trạng thái cuối cùng trong SQLite; ảnh số pending một mình không đủ.
- **M08:** lưu kết quả claim của cả hai thiết bị và log backend chứng minh chỉ có một Primary.
- Chỉ tắt Force Offline sau khi đã ghi nhận kết quả. Không xóa cache nếu còn sự kiện pending/conflict cần điều tra.

#### Giới hạn hiện tại cần biết trước khi nghiệm thu

- Backend của repository chưa có các endpoint scanner cần cho manifest và đồng bộ (`/api/v1/scanner/manifest`, `/api/v1/tickets/sync-offline`) và claim Primary (`/api/v1/scanner/claim-primary`). Vì vậy, hiện chưa thể hoàn tất kiểm thử end-to-end trên điện thoại chỉ với repository này.
- Ô địa chỉ backend trong tab **Ca Trực** hiện chưa được áp dụng vào service đồng bộ. Trên điện thoại Android, `localhost` trỏ về chính điện thoại, không phải máy tính đang chạy backend.
- Luồng claim Primary chưa được gọi từ giao diện. M08 cần backend và giao diện hỗ trợ trước khi có thể nghiệm thu end-to-end trên điện thoại.
- Khi API đồng bộ không truy cập được, sự kiện vẫn ở trạng thái chờ và được ghi nhận lần gửi lại; chỉ ACK đúng sự kiện từ backend mới chuyển sự kiện sang `Synced`.
