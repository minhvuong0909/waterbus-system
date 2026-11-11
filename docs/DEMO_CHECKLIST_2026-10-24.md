# Checklist & kịch bản demo — Smart Waterbus (kiểm tra ngày 23/10/2026)

Môi trường kiểm tra: `docker compose up -d --build` (Web :8080), code nhánh `fix/demo-check` (main + 3 commit sửa).
Ký hiệu: ✅ chạy đúng · ⚠️ chạy nhưng có điểm yếu · ❌ không dùng được · 🔧 đã sửa trong nhánh này.

## 0. Việc BẮT BUỘC làm trước giờ demo

| # | Việc | Ai |
|---|------|----|
| 1 | Điền `VNPAY_TMN_CODE` và `VNPAY_HASH_SECRET` (sandbox, đăng ký miễn phí ở sandbox.vnpayment.vn) vào `.env`. Thiếu 2 key này thì **không thanh toán được** | Bạn |
| 2 | `docker compose up -d --build`, đợi API healthy | Bạn |
| 3 | Mở trình duyệt, **Ctrl+F5** một lần (bản cũ đã bị cache trước khi sửa nginx) | Bạn |
| 4 | Chạy thử 1 lần thanh toán VNPay sandbox thật trước khi lên lớp (chưa kiểm được vì chưa có key) | Bạn |
| 5 | Chuyến có sẵn chỉ đến **25/10** (24/10: 08:00, 15:00, 18:00 giờ VN). Không có API sinh chuyến mới | Lưu ý |

Đã làm sẵn (không nằm trong git): thêm `Qr__PrivateKeyPem` vào `.env` (thiếu khoá này thì trang "Đặt chỗ thành công" sập 500), gán `captain@waterbus.vn` cho tàu SWB-01 trong DB.

## 1. Build & test

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` + `dotnet test` (có SQL thật) | ✅ 672 pass, 2 skip (cần 3 Redis độc lập), 0 fail |
| `pnpm lint` / `pnpm test` / `pnpm build` | ✅ lint sạch, 164/164 test, build OK |
| `docker compose up -d --build` | ✅ 4 container chạy, API healthy |

## 2. Guest / Passenger

| Chức năng | KQ | Ghi chú |
|---|---|---|
| Trang chủ, tìm chuyến (hết chuyến hôm nay → tự gợi ý ngày mai) | ✅ | |
| Chi tiết chuyến | ⚠️ | Kết quả tìm ghi 15:00→15:45 (45'), chi tiết ghi 15:02→15:44 (42'). Lệch nhỏ, có thể bị hỏi |
| Sơ đồ ghế, chọn ghế, giữ chỗ 10 phút | ✅ | `POST /checkouts/holds` 201 |
| Thông tin hành khách, xác nhận, đồng ý quy tắc | ✅ | |
| Chuyển sang VNPay sandbox | ❌ | Thiếu key VNPay trong `.env` → VNPay báo lỗi `code=03` |
| IPN xác nhận → phát vé (`Ticket.Issue`) | ✅ | Kiểm bằng IPN mô phỏng: đơn Confirmed, 1 vé |
| Trang đặt chỗ thành công | ✅ | Sau khi thêm khoá ký QR |
| Vé của tôi, vé QR, quản lý đặt chỗ | ⚠️ | Vé chỉ lưu trong `sessionStorage`: **đóng tab là mất**, không có email gửi link |
| Link bảo mật `/manage/:token?orderId=` | ✅ | Mở được khi đã xoá dữ liệu phiên |
| Link sai/hết hạn | 🔧 | Trước hiện "kiểm tra kết nối", nay hiện "Liên kết không hợp lệ hoặc đã hết hạn" |
| Chống đặt trùng ghế đã bán | ✅ | API trả 409 ProblemDetails có `traceId` (chưa thử 2 tab trên UI) |
| Trợ lý AI | ✅ | Trả lời kèm 3 chuyến ngày mai |
| Khám phá, Trợ giúp | ✅ | |
| Theo dõi tàu (khách) | ⚠️ | Chỉ hiện "sắp ra mắt", vị trí tàu mới có cho Admin |
| Đăng nhập Google / Quên mật khẩu / Tạo tài khoản | ❌ | Chỉ có giao diện, BE không có API. Không bấm khi demo |

## 3. Admin (`admin@waterbus.vn`)

| Chức năng | KQ | Ghi chú |
|---|---|---|
| Đăng nhập, Tổng quan | ✅ | Chuyến đã qua giờ vẫn ghi "Theo lịch" |
| Bến: thêm / sửa / ngừng hoạt động | ✅ | Soft delete, đã dọn bến test ST99 |
| Tàu: thêm / ngừng hoạt động | ⚠️ | Không có nút **sửa**, không chọn được thuyền trưởng; tàu mới **không sinh ghế** (0 ghế thật) |
| Lịch chạy | ❌ | BE chưa có API, trang ghi "Chờ BE hỗ trợ" |
| Giá vé | ❌ | BE chưa có API quản lý bảng giá |
| Chuyến | ⚠️ | Chỉ xem; không sinh chuyến, không đổi tàu |
| Sự cố: cảnh báo thời tiết, "Kiểm tra thời tiết ngay" | ✅ | Idempotent: 0 mới, 1 cập nhật. Ngưỡng gió `.env` = 0.5 m/s nên luôn có cảnh báo |
| Quyết định Tạm hoãn → Tiếp tục | ✅ | Ghi `TripOperationEvent` + nhật ký; Incident không tự đổi `Trip.Status` |
| Đóng cảnh báo | ✅ | |
| Radar realtime | ✅ | Hiện vị trí SWB-01 từ GPS Captain và cảnh báo SOS |

## 4. Captain (`captain@waterbus.vn`)

| Chức năng | KQ | Ghi chú |
|---|---|---|
| Vào cổng thuyền trưởng | 🔧 | Trước đó báo "chưa được giao tàu". Đã gán trong DB và thêm vào seed |
| Danh sách chuyến | 🔧 | Trước chỉ có giờ (08:00 lặp 3 lần), nay có ngày |
| Chia sẻ GPS | ✅ | Kiểm bằng toạ độ giả lập; server lưu DB + Redis |
| Bắt đầu chuyến → Đã cập bến | ✅ | |
| SOS | ✅ 🔧 | Câu cảnh báo/xác nhận trước bị rơi về tiếng Anh, đã dịch lại |

## 5. Lỗi đã sửa trong nhánh `fix/demo-check`

1. **Bảo mật (nghiêm trọng)**: khi `.env` để trống key VNPay, BE ký/kiểm IPN bằng khoá rỗng nên ai cũng tự ký được IPN "đã thanh toán" → nhận vé miễn phí (đã tái hiện được). Nay thiếu key thì từ chối. Có 4 test mới.
2. Seed gán thuyền trưởng mặc định cho SWB-01.
3. Dịch lại 3 câu SOS/tạm ngưng của Captain; hiện ngày trong danh sách chuyến Captain.
4. Thông báo link quản lý sai/hết hạn.
5. nginx: `index.html` gửi `Cache-Control: no-cache` để build mới có hiệu lực ngay.

## 6. Lỗi lớn, chưa sửa (cần bạn quyết định)

- Thiếu key VNPay sandbox (bắt buộc cho demo, chỉ bạn làm được).
- BE thiếu API: tuyến, lịch chạy, giá vé, sinh chuyến, đổi tàu, gán thuyền trưởng, đăng ký tài khoản. Không nên làm gấp trong một đêm.
- Vé Guest chỉ lưu trong tab hiện tại, không có kênh gửi link bảo mật cho khách.
- Chạy localhost thì VNPay không gọi được IPN về máy. Vé phụ thuộc bước đối soát (reconcile) phía FE, **chưa kiểm với VNPay thật**.

## 7. Kịch bản demo 10 phút (theo thứ tự ưu tiên)

1. **(0:30) Kiến trúc**: `docker compose` gồm SQL Server, Redis, API .NET 8 (Clean Architecture), Web React. Tất cả đi qua nginx cùng origin.
2. **(3:00) Guest đặt vé**: Trang chủ → Tìm chuyến (24/10) → Chi tiết → Chọn ghế (nói: giữ 10 phút, Redis lock + RowVersion) → Thông tin → Xác nhận → VNPay sandbox (thẻ test NCB của VNPay) → Đặt chỗ thành công → Vé QR (ký RS256, Staff quét offline) → Quản lý đặt chỗ. **Không đóng tab.**
3. **(1:00) Chống đặt trùng**: ở trình duyệt Guest khác, chọn đúng ghế vừa mua → ghế đã bán/409. Nên tập trước, vì luồng 2 tab trên UI chưa được kiểm.
4. **(2:00) Captain**: đăng nhập trong cửa sổ ẩn danh Chrome → Chia sẻ GPS (trình duyệt hỏi quyền vị trí; GPS laptop có thể lệch xa sông) → Bắt đầu chuyến → SOS.
5. **(2:00) Admin radar + sự cố**: Radar thấy tàu và SOS realtime → Sự cố → "Kiểm tra thời tiết ngay" → Tạm hoãn một chuyến kèm lý do → Nhật ký thao tác. Nhấn mạnh: hệ thống chỉ cảnh báo, Admin mới quyết định.
6. **(1:00) Admin CRUD bến** (thêm → sửa → ngừng hoạt động) + trợ lý AI hỏi "ngày mai có chuyến nào".
7. **(0:30) Kỹ thuật**: 672 test BE + 164 test FE xanh, ProblemDetails, phân quyền theo role.

**Tránh mở**: Lịch chạy, Giá vé, Theo dõi tàu (phía khách), nút Google / Quên mật khẩu / Tạo tài khoản, sửa tàu.
Phiên đăng nhập lưu trong `localStorage`, nên hai tab cùng trình duyệt sẽ đè phiên của nhau. Cách tách vai trò: Admin ở Chrome thường, Captain ở cửa sổ ẩn danh Chrome, Guest ở trình duyệt khác (Edge). Các cửa sổ ẩn danh Chrome dùng chung **một** phiên với nhau.
