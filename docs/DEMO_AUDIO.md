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
1. Mở http://localhost:8080/sign-in, đăng nhập Captain `captain@waterbus.vn` / `Captain@123456!` (tài khoản seed, chỉ có ở Development).
2. Vào Cổng thuyền trưởng (`/captain`). Bấm **Bật loa** một lần (trình duyệt chỉ cho phát âm thanh sau một cú chạm).
3. Chọn một chuyến tham quan ("… (Tham quan)" hoặc "Tour vòng sông Sài Gòn") và bấm **Mở đón khách**.
   Trước khi **Khởi hành**, Admin phải xác nhận Captain có mặt cho chuyến đó (quản lý Captain của teammate: Admin đăng nhập, mở chuyến, ghi nhận có mặt;
   API `POST /api/v1/trips/{tripId}/captain-attendance`). Chưa xác nhận thì Khởi hành bị từ chối. Sau đó bấm **Khởi hành** (mỗi nút xác nhận qua hộp thoại).
4. Nghe: "rời bến", các địa danh (chỉ chuyến tham quan), "sắp tới bến", "đã tới bến". Mỗi thông báo phát tiếng Việt rồi tiếng Anh.
5. Khi tàu tới bến cuối nút **Đã cập bến** bật lên, bấm xong thì bấm **Kết thúc chuyến**.
6. Admin (`admin@waterbus.vn` / `Admin@123456!`) xem tàu chạy ở **Điều hành trực tiếp**.

Chỉ một trình phát nhận audio cho mỗi tàu: **kết nối mới nhất thắng**. Nếu hai người cùng đăng nhập Captain để demo thì chỉ người mở sau nghe được; đóng tab cũ.

## Lỗi thường gặp
| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Không có tàu chạy trên bản đồ | Chưa bật giả lập GPS (thiếu file `docker-compose.demo.yml`), hoặc chuyến chưa được Bắt đầu. |
| Nút "Mở đón khách" xám, ghi "Đến hh:mm mới mở" | Chưa dùng file demo (mặc định chỉ mở từ 30 phút trước giờ khởi hành). |
| "Đã cập bến" xám, ghi "Chờ GPS" hoặc "chưa tới bến cuối" | Tàu chưa tới bến cuối; cần có GPS (giả lập hoặc Chia sẻ GPS). Admin vẫn cho cập bến được. |
| Bấm Khởi hành báo "Admin chưa ghi nhận Captain có mặt" | Admin cần xác nhận Captain có mặt cho chuyến (xem bước 3). |
| Có chuyến chạy nhưng im lặng | Chưa bấm **Bật loa**; hoặc API chưa seed xong clip (đợi thêm); hoặc tab Captain khác đang nhận audio. |
| Địa danh không phát hết | Hệ thống xét địa danh lần lượt theo thứ tự tuyến; xem bán kính ở `ApplicationDbContextInitializer.SightseeingPois`. |

## Đổi nội dung audio
Sửa câu chữ trong `tools/audio/texts.json`, rồi chạy (cần Internet, Python 3):
```bash
pip install edge-tts mutagen
python tools/audio/generate_audio.py
```
Script ghi lại các MP3 và `manifest.json` ở thư mục seed; build lại API để nạp. Bến hoặc địa danh Admin thêm sau không có clip nên không phát (chỉ ghi log).
