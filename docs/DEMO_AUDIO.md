# Demo audio trên tàu (thông báo bến + thuyết minh địa danh)

Dành cho bất kỳ ai trong nhóm muốn chạy lại demo. Không cần key dịch vụ ngoài, không cần cài Python: các file MP3 (tiếng Việt rồi tiếng Anh)
đã nằm trong repo (`waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/SeedData/Audio`) và được nạp vào bảng `AudioClips` khi API khởi động.

## Chạy
```bash
docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build
```
`docker-compose.demo.yml` bật môi trường Development (để seed dữ liệu mẫu), bộ giả lập GPS (một vòng chuyến 210 giây, tàu dừng ở mỗi bến),
kích hoạt audio theo vị trí và cho Captain mở đón khách sớm. Đợi khoảng **1 phút** sau khi API lên để seed xong (tuyến tham quan SS01/SS02, địa danh, clip).

Chạy API không bằng Docker: đặt cùng các biến môi trường trong file override đó (`Tracking__Simulator__*`, `SightseeingAudio__EnableGpsTriggerEvaluation=true`,
`CaptainOperations__OpenBoardingLeadMinutes=10080`, `ASPNETCORE_ENVIRONMENT=Development`).

## Các bước demo (chỉ thao tác trên web)
Dữ liệu mẫu đã chuẩn bị sẵn, không cần chạy lệnh nào ngoài lệnh khởi động ở trên:
- **Tàu SWB-01 là tàu tham quan** (thuyền trưởng `captain@waterbus.vn`): chỉ chạy hai tuyến SS01 Bạch Đằng → Linh Đông và SS02 Linh Đông → Bạch Đằng
  (không dừng bến giữa, đi qua 5 địa danh).
- **Tàu SWB-02 là tàu chuyến thường** (thuyền trưởng `captain2@waterbus.vn`): chạy RT01/RT02 nhiều bến.
- Mật khẩu cả hai: `Captain@123456!`. Thuyền trưởng gắn cố định với tàu của mình, Admin phân tàu cho từng chuyến, nên trang Captain chỉ hiện
  **chuyến hiện tại của tàu** (không có ô chọn chuyến).
- Seed (Development) đã điểm danh sẵn hai thuyền trưởng cho các chuyến sắp chạy, như thể Admin đã xác nhận có mặt.

**Demo tàu tham quan (có địa danh):**
1. Mở http://localhost:8080/sign-in, đăng nhập `captain@waterbus.vn`. Trang `/captain` hiện chuyến tham quan sắp chạy.
2. Bấm một chỗ bất kỳ trên trang (trình duyệt chỉ cho phát tiếng sau một cú chạm; loa tự mở, không có nút riêng).
3. Bấm **Mở đón khách**, rồi **Khởi hành** (mỗi nút có hộp thoại xác nhận).
4. Nghe: "rời bến", năm địa danh (Landmark 81, Cầu Sài Gòn, Thảo Điền, Thanh Đa, Bình Quới) dọc đường, rồi "sắp tới bến" và "đã tới bến".
   Mỗi thông báo phát tiếng Việt trước, tiếng Anh sau; góc dưới bên phải hiện dải ghi tên bến/địa danh đang đọc. Bản đồ cho thấy tàu của mình chạy qua các ghim cam.
5. Khi tàu tới bến cuối nút **Đến bến cuối** bật lên, bấm xong thì bấm **Kết thúc chuyến**; trang tự chuyển sang chuyến kế tiếp.

**Demo tàu thường (thông báo từng bến):** làm như trên nhưng đăng nhập `captain2@waterbus.vn`. Nghe: bến đầu "rời bến"; mỗi bến giữa "sắp tới", "đã tới", "rời bến"; bến cuối "sắp tới", "đã tới".

**Xem phía Admin:** đăng nhập `admin@waterbus.vn` / `Admin@123456!` ở cửa sổ khác, mở **Điều hành trực tiếp** để thấy tàu chạy và các mốc địa danh.

Một vòng chạy khoảng 3,5 phút. Chỉ một trình phát nhận audio cho mỗi tàu: **kết nối mới nhất thắng**; nếu hai người cùng đăng nhập một tài khoản Captain thì chỉ người mở sau nghe được, hãy đóng tab cũ.

## Lỗi thường gặp
| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Không có tàu chạy trên bản đồ | Chưa bật giả lập GPS (thiếu file `docker-compose.demo.yml`), hoặc chuyến chưa được Bắt đầu. |
| Nút "Mở đón khách" xám, ghi "Đến hh:mm mới mở" | Chưa dùng file demo (mặc định chỉ mở từ 30 phút trước giờ khởi hành). |
| "Đã cập bến" xám, ghi "Chờ GPS" hoặc "chưa tới bến cuối" | Tàu chưa tới bến cuối; cần có GPS (giả lập hoặc Chia sẻ GPS). Admin vẫn cho cập bến được. |
| Bấm Khởi hành báo "Admin chưa ghi nhận Captain có mặt" | Chuyến mới sinh sau lần khởi động API chưa được điểm danh sẵn; khởi động lại API (seed chạy lại và điểm danh bổ sung) hoặc để Admin xác nhận có mặt. |
| Có chuyến chạy nhưng im lặng | Chưa bấm hoặc gõ phím nào trên trang Captain; chuyến này đã phát hết (mỗi thông báo chỉ phát một lần cho mỗi chuyến, hãy chạy chuyến mới); hoặc API chưa seed xong clip (đợi thêm); hoặc tab Captain khác đang nhận audio. |
| Địa danh không phát hết | Hệ thống xét địa danh lần lượt theo thứ tự tuyến; xem bán kính ở `ApplicationDbContextInitializer.SightseeingPois`. |

## Đổi nội dung audio
Sửa câu chữ trong `tools/audio/texts.json`, rồi chạy (cần Internet, Python 3):
```bash
pip install edge-tts mutagen
python tools/audio/generate_audio.py
```
Script ghi lại các MP3 và `manifest.json` ở thư mục seed; build lại API để nạp. Bến hoặc địa danh Admin thêm sau không có clip nên không phát (chỉ ghi log).
