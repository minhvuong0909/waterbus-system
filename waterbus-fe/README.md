# Smart Waterbus — Web (React + Vite + TypeScript)

Web của hệ thống **Smart Waterbus** cho Passenger/Guest (tìm chuyến, đặt vé, thanh toán VNPay, vé QR),
Admin (vận hành, sự cố, hoàn tiền, cấu hình) và Captain (Start/Arrive, SOS, gửi GPS). App Staff Scanner
nằm riêng ở `waterbus-mobile/`. Nghiệp vụ theo `docs/Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md`
(BR v2.0); quy ước code ở `AGENTS.md` và `docs/CONVENTIONS.md` mục 4.

## Chức năng

- **Guest/Passenger:** tìm chuyến theo bến lên, bến xuống, ngày, một chiều hoặc khứ hồi; xem chi tiết chuyến
  (lộ trình, khung check-in của bến lên); chọn ghế theo đoạn với cập nhật ghế realtime (SignalR); nhập người
  mua và hành khách; thanh toán VNPay (vé chỉ phát sau khi backend xác nhận). Không cần tài khoản: đơn đã mua
  nằm trong "Vé của tôi" trên thiết bị, và mở lại ở máy khác bằng link quản lý vé.
- **Admin:** tổng quan, điều hành trực tiếp (bản đồ tàu), danh sách chuyến theo trạng thái, bến, tàu, giá vé
  (xem), sự cố và điều hành chuyến (Admin quyết định Suspend/Resume/Cancel/Terminate cho từng chuyến, sự cố
  không tự đổi trạng thái chuyến), hoàn tiền, khung giờ check-in theo bến, thuyết minh ngắm cảnh (tạo audio
  tiếng Việt, xuất bản cho POI).
- **Captain:** Start/Arrived chuyến của tàu mình, gửi SOS, chia sẻ GPS.
- **Ngắm cảnh:** trang Khám phá giới thiệu các POI có thuyết minh và các chuyến ngắm cảnh sắp chạy. Thuyết
  minh phát qua loa trên tàu, web không phát audio cho hành khách (BR-EXP-02).

## Cấu trúc `src/`

```text
src/
├── pages/        # Màn hình theo route, chỉ ghép component của feature
├── features/     # Nghiệp vụ: components/, hooks/, services/, types.ts, utils.ts, slice Redux
│   ├── admin/  assistant/  authentication/  booking/  captain/
│   └── explore/  home/  payment/  tickets/  tracking/  trips/
├── components/   # UI dùng chung (ui/, map/, motion/, ticket/, brand/), không import feature
├── layouts/      # MainLayout, header/, footer/, admin/, captain/
├── routes/       # routes.ts (đường dẫn), AppRoutes.tsx, PrivateRoute (kiểm role)
├── store/        # Redux store
├── services/     # apiClient (Accept-Language, JWT, ProblemDetails), realtime (SignalR), authStorage
├── hooks/        # useFetch, useDocumentTitle, usePopoverList, usePrefersReducedMotion
├── constant/     # apiEndpoints.ts, env.ts, roles.ts
├── content/      # Nội dung biên tập không có API (FAQ, quy tắc vận chuyển, giới thiệu bến, POI)
├── i18n/         # Tiếng Anh (nguồn) và từ điển tiếng Việt
├── utils/        # Định dạng tiền vi-VN, giờ Việt Nam, JWT, GUID
└── assets/       # Ảnh, video, font icon (đã cắt gọn, xem assets/fonts/README.md)
```

Phụ thuộc giữa các feature chỉ một chiều (vd `booking → trips`, `tickets → booking`), không có vòng.
Danh sách route: `docs/ROUTES.md`. Endpoint và service tương ứng: `docs/API_INTEGRATION.md`.

## Môi trường

Sao chép `.env.example` thành `.env.local`:

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `VITE_API_BASE_URL` | rỗng (cùng origin) | Gốc API backend, không có `/` cuối. Để rỗng khi chạy sau nginx hoặc proxy của Vite |
| `VITE_MAP_TILE_URL`, `VITE_MAP_TILE_ATTRIBUTION` | bản đồ Esri | Nguồn tile bản đồ (tuỳ chọn) |

Khi `pnpm dev`, Vite proxy `/api` và `/hubs` sang backend ở `http://127.0.0.1:5000`.

## Lệnh (dùng pnpm)

```bash
pnpm install --frozen-lockfile
pnpm dev          # http://localhost:5173
pnpm lint
pnpm test
pnpm build        # gồm tsc
```

Cả hệ thống (backend, SQL Server, Redis, web ở cổng 8080): `docker compose up -d --build` ở thư mục gốc repo.

## Quy ước

- Mọi API khai báo ở `src/constant/apiEndpoints.ts` và gọi qua service trong `src/features/<x>/services`.
- Lỗi backend là ProblemDetails; `apiClient` chuyển thành `ApiError` (có `status`, `fieldErrors`).
- Thời gian nhận UTC, hiển thị giờ Việt Nam; tiền định dạng `vi-VN`.
- 5 actor: Passenger, Guest, Admin, Staff, Captain (không có Dispatcher). Guest dùng link bảo mật, không cần
  đăng nhập.
