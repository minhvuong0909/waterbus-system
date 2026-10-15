# Cấu hình dịch vụ AI của Thịnh (không commit key)

Code độc lập hiện dùng OpenAI qua Semantic Kernel, Azure AI Speech cho MP3 tiếng Việt và Azure Blob Storage cho MP3. **Có key không đồng nghĩa luồng chạy thật đã nối xong**: AI còn chờ Trip Search/booking-link đáng tin và Web gọi API; audio còn chờ kịch bản đã duyệt, AudioGuide và Onboard Player.

## 1. Azure Speech key, region và voice

1. Trong [Azure portal](https://portal.azure.com/), tạo hoặc mở **Speech resource** được nhóm cho dùng.
2. Vào **Keys and Endpoint** để lấy key và **Region identifier** của chính resource đó. Key và region phải cùng resource/vùng. [Microsoft: Speech key/endpoint](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech), [region](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/regions).
3. Chọn một voice `vi-VN-...Neural` có trong [danh sách Azure](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts), ví dụ `vi-VN-HoaiMyNeural`. Đây là lựa chọn cấu hình, không phải BR của nhóm.
4. Chọn định dạng MP3 của Speech SDK, ví dụ `Audio16Khz32KBitRateMonoMp3`.

Từ thư mục `waterbus-be`, cấu hình WebApi local bằng User Secrets (thay placeholder trên máy của bạn):

```powershell
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "<speech-key>" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "AzureSpeech:Region" "<region-id>" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "AzureSpeech:DefaultVoiceName" "vi-VN-HoaiMyNeural" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "AzureSpeech:OutputFormat" "Audio16Khz32KBitRateMonoMp3" --project src/WaterbusSystem.WebApi
```

Smoke test TTS hiện có đọc **biến môi trường**, không đọc User Secrets. Xem [TTS_LIVE_SMOKE_TEST.md](TTS_LIVE_SMOKE_TEST.md); chỉ bật khi bạn chấp nhận gọi Azure và dùng quota/chi phí. Unit test mặc định không gọi Azure.

## 2. Azure Blob connection string

1. Tạo hoặc mở **Storage account** được nhóm cho dùng trong Azure portal.
2. Trong **Data storage → Containers**, tạo `audio-guides-private` với **Private (no anonymous access)**. Code không tự tạo container hoặc bật public access. [Microsoft: private container](https://learn.microsoft.com/en-us/azure/private-link/tutorial-private-endpoint-storage-portal#add-a-blob-container).
3. Trong **Security + networking → Access keys**, sao chép **Connection string** (không phải chỉ Key) nếu bạn có quyền. Đây là secret quyền rộng trên account; nên dùng tài khoản dev/test riêng và xoay key nếu bị lộ. [Microsoft: connection string](https://learn.microsoft.com/en-us/azure/storage/common/storage-account-keys-manage). Nếu nhóm không cho dùng Shared Key, cần đổi adapter sang Microsoft Entra/managed identity theo quyết định hạ tầng.

```powershell
dotnet user-secrets set "AzureBlobAudioStorage:ConnectionString" "<storage-connection-string>" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "AzureBlobAudioStorage:ContainerName" "audio-guides-private" --project src/WaterbusSystem.WebApi
```

Adapter chỉ nhận MP3 hợp lệ, từ chối ghi đè cùng `AssetKey`, trả URI Blob không có SAS và không tự publish `AudioGuide`. Nếu sửa script/voice, caller phải cấp **AssetKey phiên bản mới**. Container private cần cơ chế cấp quyền đọc ngắn hạn cho Onboard Player ở bước tích hợp; URI lưu DB tự nó không phát được.

`IAudioAssetGenerationService` đã nối TTS → Blob bằng kịch bản đã duyệt và `AssetKey` do caller cung cấp. Service chỉ trả metadata/URI; không tạo hoặc publish `AudioGuide`, không tự chọn version kịch bản.

## 3. OpenAI API key

Tạo API key trong [OpenAI Platform](https://platform.openai.com/api-keys) cho project được nhóm cho phép dùng; kiểm tra ngân sách/quota trước khi bật. [OpenAI API quickstart](https://developers.openai.com/api/docs/quickstart). Không suy ra ChatGPT subscription đã có API credit, và không gửi key qua chat.

```powershell
dotnet user-secrets set "AiBookingAssistant:OpenAI:ApiKey" "<openai-api-key>" --project src/WaterbusSystem.WebApi
dotnet user-secrets set "AiBookingAssistant:OpenAI:ModelId" "gpt-4.1-mini" --project src/WaterbusSystem.WebApi
```

**Chưa bật `IsEnabled=true` chỉ vì đã có key.** `IAiBookingAssistantService` chưa được đăng ký vào API vì Trip Search hiện trả giá `0`/stop giả định và booking-link tool production chưa được owner xác nhận. Giữ `IsEnabled=false` đến khi contract được nối và kiểm thử.

## 4. Docker Compose và kiểm thử thật

Ở gốc repo, tạo `.env` từ `.env.example`, chỉ điền vào file local này các biến `AZURE_SPEECH_*`, `AZURE_BLOB_*`, `OPENAI_API_KEY`. Docker Compose chuyển chúng vào WebApi theo `docker-compose.yml`; `.env` nằm trong Git ignore. Để `AI_BOOKING_ENABLED=false` cho đến khi backend AI nối an toàn. Không dán `.env` hoặc `dotnet user-secrets list` vào báo cáo.

- Azure Speech: live smoke tạo MP3 hợp lệ, nghe được; kết quả không tự lưu Blob.
- Azure Blob: cần thử upload một asset thử nghiệm vào container private và đọc lại; unit test với mock không chứng minh cloud hoạt động.
- OpenAI: chỉ live test sau khi tool Trip Search và booking link dùng dữ liệu đúng BR FINAL.
- GPS/Onboard Player: cần contract dispatch, ACK và safety override; unit test geofence chưa chứng minh loa tàu đã phát.
