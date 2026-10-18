# Cấu hình dịch vụ AI của Thịnh (không commit key)

Code độc lập hiện dùng Groq qua Semantic Kernel, Azure AI Speech cho MP3 tiếng Việt và Azure Blob Storage cho MP3. **Có key không đồng nghĩa luồng chạy thật đã nối xong**: AI còn chờ Trip Search/booking-link đáng tin và Web gọi API; audio còn chờ kịch bản đã duyệt, AudioGuide và Onboard Player.

## 1. Azure Speech key, region và voice

1. Trong [Azure portal](https://portal.azure.com/), tạo hoặc mở **Speech resource** được nhóm cho dùng.
2. Vào **Keys and Endpoint** để lấy key và **Region identifier** của chính resource đó. Key và region phải cùng resource/vùng. [Microsoft: Speech key/endpoint](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech), [region](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/regions).
3. Chọn một voice `vi-VN-...Neural` có trong [danh sách Azure](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts), ví dụ `vi-VN-HoaiMyNeural`. Đây là lựa chọn cấu hình, không phải BR của nhóm.
4. Chọn định dạng MP3 của Speech SDK, ví dụ `Audio16Khz32KBitRateMonoMp3`.

Từ thư mục `waterbus-be`, cấu hình WebApi local bằng User Secrets. Dán key khi PowerShell hỏi; ký tự không hiện là bình thường:

```powershell
$speechKey = Read-Host "Dán Azure Speech Key 1" -MaskInput
dotnet user-secrets set "AzureSpeech:SubscriptionKey" $speechKey --project src/WaterbusSystem.WebApi/WaterbusSystem.WebApi.csproj
Remove-Variable speechKey
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

## 3. Groq API key

Tạo API key trong [Groq Console](https://console.groq.com/keys); kiểm tra [rate limit/quota](https://console.groq.com/docs/rate-limits) trước khi bật. Groq là quyết định mới của Thịnh, khác OpenAI/Gemini ghi trong Drive: báo leader/nhóm để xác nhận trước khi tích hợp chính thức. Không gửi key qua chat.

```powershell
$groqKey = Read-Host "Dán Groq API key" -MaskInput
dotnet user-secrets set "AiBookingAssistant:Groq:ApiKey" $groqKey --project src/WaterbusSystem.WebApi
Remove-Variable groqKey
dotnet user-secrets set "AiBookingAssistant:Groq:ModelId" "openai/gpt-oss-20b" --project src/WaterbusSystem.WebApi
```

Key cũ `AiBookingAssistant:OpenAI:ApiKey` (nếu còn trong User Secrets) **không được adapter Groq đọc**. Không cần đưa key Groq cho frontend; chỉ backend giữ key. Nếu đã lưu `AiBookingAssistant:Groq:ModelId=llama-3.3-70b-versatile`, phải chạy lại lệnh ModelId ở trên: User Secrets ưu tiên hơn giá trị mặc định mới của code.

**Chưa bật `IsEnabled=true` chỉ vì đã có key.** `IAiBookingAssistantService`, hai tool adapter và `POST /api/v1/AiAssistant/chat` đã có trong backend, nhưng mặc định tắt. Trip Search hiện trả giá `0`/stop giả định; booking-link template chưa được owner Web/Booking xác nhận. Hai cờ `AiBookingAssistant:Integration:IsTripSearchContractVerified` và `IsBookingLinkContractVerified` mặc định `false`; không tự bật để lấy Trip Card production sai.

Có thể thử **Groq thật + Semantic Kernel function calling với trip fixture test** (không dùng Trip Search/Booking thật):

```powershell
$env:WATERBUS_GROQ_BOOKING_SMOKE = '1'
dotnet test WaterbusSystem.sln --filter FullyQualifiedName~GroqBookingLiveSmokeTests
Remove-Item Env:WATERBUS_GROQ_BOOKING_SMOKE
```

Chạy trong `waterbus-be/` sau khi lưu key bằng User Secrets như trên. Test opt-in này dùng quota Groq (và có thể phát sinh chi phí tùy tài khoản); pass chỉ chứng minh provider/plugin hoạt động với dữ liệu fixture, **không** chứng minh giá/bến/link/UI chạy thật. Không in hoặc gửi key trong chat/log.

Nếu test trả `IsFallback=true`, dòng assert hiện cho biết **loại exception và HTTP status**, không in key hoặc nội dung phản hồi. HTTP 429 có thể là hết quota hoặc chạm rate limit; kiểm tra limits của đúng Groq project, không retry liên tục. Nếu `tools=` rỗng, request đã lỗi trước khi function `SearchTrips` chạy. Xóa cờ PowerShell bằng `Remove-Item Env:WATERBUS_GROQ_BOOKING_SMOKE` (không có dấu `?`).

## 4. Docker Compose và kiểm thử thật

Ở gốc repo, tạo `.env` từ `.env.example`, chỉ điền vào file local này các biến `AZURE_SPEECH_*`, `AZURE_BLOB_*`, `GROQ_API_KEY`. Docker Compose chuyển chúng vào WebApi theo `docker-compose.yml`; `.env` nằm trong Git ignore. Để `AI_BOOKING_ENABLED=false` cho đến khi backend AI nối an toàn. Không dán `.env` hoặc `dotnet user-secrets list` vào báo cáo.

- Azure Speech: live smoke xác nhận Azure trả bytes có header MP3 và thời lượng dương; chưa nghe kiểm tra chất lượng giọng, kết quả không tự lưu Blob.
- Azure Blob: có `AzureBlobLiveSmokeTests` opt-in để upload một blob thử nghiệm vào container private, kiểm tra tồn tại/đọc lại đúng bytes và xóa blob do chính test tạo. Test mặc định không gọi Azure; bytes thử chỉ có chữ ký ID3, **không phải MP3 nghe được**. Từ `waterbus-be`, sau khi lưu WebApi User Secrets ở trên, bật cờ trong đúng PowerShell đang chạy test:

  ```powershell
  $env:WATERBUS_AZURE_BLOB_SMOKE = "1"
  dotnet test WaterbusSystem.sln --filter FullyQualifiedName~AzureBlobLiveSmokeTests
  Remove-Item Env:WATERBUS_AZURE_BLOB_SMOKE
  ```

  Test chỉ được coi là đã gọi Blob thật nếu cờ bằng `1`; có thể tốn một lượng nhỏ phí giao dịch/lưu trữ. Nếu fail, kiểm tra container đã tồn tại, connection string đúng và Storage Account cho phép truy cập từ máy local. Không gửi connection string hoặc log chứa secret.
- TTS → Blob bằng **MP3 nghe được**: `AzureAudioPipelineLiveSmokeTests` gọi Azure Speech thật qua `AudioAssetGenerationService`, upload Blob, tải lại và đối chiếu từng byte. Test xóa blob thử trên Azure sau khi chạy và giữ một bản MP3 trong `%LOCALAPPDATA%\Waterbus\smoke\` để nghe; không tạo/publish `AudioGuide`. Cần lưu cả bốn `AzureSpeech:*` và hai `AzureBlobAudioStorage:*` trong WebApi User Secrets (hoặc biến môi trường tương ứng). Từ `waterbus-be`:

  ```powershell
  $env:WATERBUS_AZURE_AUDIO_PIPELINE_SMOKE = "1"
  dotnet test WaterbusSystem.sln --filter FullyQualifiedName~AzureAudioPipelineLiveSmokeTests
  Remove-Item Env:WATERBUS_AZURE_AUDIO_PIPELINE_SMOKE
  Get-ChildItem "$env:LOCALAPPDATA\Waterbus\smoke\azure-audio-*.mp3" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  ```

  Mở file MP3 mới nhất để nghe. Cờ phải bằng `1` trong đúng phiên PowerShell chạy test; nếu không, test pass nhưng **không gọi Azure**. Bài này sử dụng quota Azure Speech và giao dịch Azure Blob. File local là dữ liệu thử ngoài repo; bạn có thể xóa sau khi nghe.
- Groq: có thể smoke test provider/plugin độc lập bằng fixture; live test **end-to-end với dữ liệu thật** chỉ sau khi owner xác nhận Trip Search và booking link, rồi Viễn nối UI.
- GPS/Onboard Player: cần contract dispatch, ACK và safety override; unit test geofence chưa chứng minh loa tàu đã phát.
