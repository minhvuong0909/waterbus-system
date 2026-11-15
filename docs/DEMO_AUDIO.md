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

## Các bước demo
Có hai tuyến tham quan một chiều: **SS01** Bạch Đằng → Linh Đông và **SS02** Linh Đông → Bạch Đằng (không dừng bến giữa, đi qua 5 địa danh).
Thuyền trưởng gắn cố định với tàu SWB-01 và chỉ thấy **chuyến hiện tại của tàu** (không có ô chọn chuyến); Admin là người phân tàu cho từng chuyến.

1. **Chuẩn bị chuyến tham quan** (Admin): `node tools/demo/prepare-sightseeing-demo.mjs` (Node 18+).
   Script chọn chuyến tham quan sắp chạy sớm nhất của SWB-01, chuyển các chuyến khác khởi hành trước nó sang tàu khác
   (`PUT /api/v1/admin/trips/{id}/boat`) và xác nhận Captain có mặt (bắt buộc trước khi Khởi hành).
2. Mở http://localhost:8080/sign-in, đăng nhập Captain `captain@waterbus.vn` / `Captain@123456!` (tài khoản seed, chỉ có ở Development).
   Trang Cổng thuyền trưởng (`/captain`) hiện đúng chuyến tham quan vừa chuẩn bị.
3. Bấm một chỗ bất kỳ trên trang (trình duyệt chỉ cho phát tiếng sau một cú chạm; loa tự mở, không có nút riêng).
4. Bấm **Mở đón khách**, rồi **Khởi hành** (mỗi nút có hộp thoại xác nhận).
5. Nghe: "rời bến", năm địa danh (Landmark 81, Cầu Sài Gòn, Thảo Điền, Thanh Đa, Bình Quới) dọc đường, rồi "sắp tới bến" và "đã tới bến".
   Mỗi thông báo phát tiếng Việt trước, tiếng Anh sau; góc dưới bên phải hiện dải ghi tên bến/địa danh đang đọc. Bản đồ cho thấy tàu của mình chạy qua các ghim cam.
6. Khi tàu tới bến cuối nút **Đến bến cuối** bật lên, bấm xong thì bấm **Kết thúc chuyến**; trang tự chuyển sang chuyến kế tiếp.
7. Admin (`admin@waterbus.vn` / `Admin@123456!`) xem tàu chạy ở **Điều hành trực tiếp**.

Một vòng chạy khoảng 3,5 phút. Muốn demo chuyến thường thì bỏ bước 1: trang hiện chuyến sắp chạy sớm nhất của tàu (bến đầu: rời bến; bến giữa: sắp tới, đã tới, rời bến; bến cuối: sắp tới, đã tới).

Chỉ một trình phát nhận audio cho mỗi tàu: **kết nối mới nhất thắng**. Nếu hai người cùng đăng nhập Captain để demo thì chỉ người mở sau nghe được; đóng tab cũ.

## Lỗi thường gặp
| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Không có tàu chạy trên bản đồ | Chưa bật giả lập GPS (thiếu file `docker-compose.demo.yml`), hoặc chuyến chưa được Bắt đầu. |
| Nút "Mở đón khách" xám, ghi "Đến hh:mm mới mở" | Chưa dùng file demo (mặc định chỉ mở từ 30 phút trước giờ khởi hành). |
| "Đã cập bến" xám, ghi "Chờ GPS" hoặc "chưa tới bến cuối" | Tàu chưa tới bến cuối; cần có GPS (giả lập hoặc Chia sẻ GPS). Admin vẫn cho cập bến được. |
| Bấm Khởi hành báo "Admin chưa ghi nhận Captain có mặt" | Chạy lại `node tools/demo/prepare-sightseeing-demo.mjs` (hoặc `confirm-attendance.mjs` cho mọi chuyến sắp chạy). |
| Có chuyến chạy nhưng im lặng | Chưa bấm hoặc gõ phím nào trên trang Captain; chuyến này đã phát hết (mỗi thông báo chỉ phát một lần cho mỗi chuyến, hãy chạy chuyến mới); hoặc API chưa seed xong clip (đợi thêm); hoặc tab Captain khác đang nhận audio. |
| Địa danh không phát hết | Hệ thống xét địa danh lần lượt theo thứ tự tuyến; xem bán kính ở `ApplicationDbContextInitializer.SightseeingPois`. |

## Đổi nội dung audio
Sửa câu chữ trong `tools/audio/texts.json`, rồi chạy (cần Internet, Python 3):
```bash
pip install edge-tts mutagen
python tools/audio/generate_audio.py
```
Script ghi lại các MP3 và `manifest.json` ở thư mục seed; build lại API để nạp. Bến hoặc địa danh Admin thêm sau không có clip nên không phát (chỉ ghi log).
