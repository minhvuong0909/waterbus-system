# Smoke test Azure Speech thật cho lát cắt 2

Lát cắt 2 có unit test bằng mock, **chưa chứng minh** Azure resource thật đã tạo MP3. Chỉ chạy bước này khi có credential dev/test và được nhóm cho phép sử dụng resource. Không commit key hoặc file audio sinh ra.

1. Trong môi trường local, đặt các biến `AzureSpeech__SubscriptionKey`, `AzureSpeech__Region`, `AzureSpeech__DefaultVoiceName` (một Azure Neural Voice tiếng Việt `vi-VN-...Neural`) và `AzureSpeech__OutputFormat` (một giá trị enum `...Mp3`). Không nhập key vào file tracked hoặc gửi qua chat.
2. Đặt `WATERBUS_AZURE_SPEECH_SMOKE=1` **chỉ trong session chạy test**.
3. Từ `waterbus-be`, chạy:

   ```powershell
   dotnet test WaterbusSystem.sln --filter FullyQualifiedName~AzureSpeechLiveSmokeTests
   ```

4. Xác nhận test gọi Azure thật, trả bytes có header ID3/MPEG, `audio/mpeg` và duration dương. Test không lưu MP3 lên disk/Blob và không chứng minh Onboard Player đã phát được.
5. Xóa các biến môi trường chứa key khỏi session sau khi thử. Trong báo cáo, ghi vùng/voice/format và kết quả test **nhưng không ghi key**.

Nếu **không** đặt `WATERBUS_AZURE_SPEECH_SMOKE=1`, test opt-in không gọi Azure; dù runner báo pass, **không được ghi “live smoke test pass”**. Khi lỗi/quota/timeout, báo mã lỗi an toàn và trao đổi với owner Azure resource; không dán raw SDK error hoặc secret vào PR.
