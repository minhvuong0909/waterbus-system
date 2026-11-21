# Phần AI & Sightseeing Audio

Tài liệu của module **AI Booking Assistant**, **Azure TTS** và **geofence kích hoạt audio tại POI**.

> Nguồn chuẩn nghiệp vụ vẫn là [Business Rules v2.0](../Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md). Khi tài liệu trong thư mục này mâu thuẫn BR, **BR thắng**.

## Phạm vi

| Có làm | Không làm |
|---|---|
| Trợ lý AI tìm chuyến (Groq + function calling, trả Trip Card) | CRUD POI, ETA tới bến |
| Azure TTS sinh MP3 tiếng Việt (`vi` là ngôn ngữ duy nhất) | Audio đa ngôn ngữ, Passenger/Captain mobile audio |
| Geofence: quyết định trigger audio đúng POI | Voucher/Wallet |

Không cho LLM tự ghi dữ liệu nghiệp vụ hay vượt quá API đã phân quyền (`BR-AI-01`). Không commit API key, connection string hoặc file audio sinh ra.

## Mục lục

| Tài liệu | Nội dung |
|---|---|
| [PROJECT_CONTEXT_AND_RULES.md](PROJECT_CONTEXT_AND_RULES.md) | Bối cảnh kiến trúc và rule bắt buộc cho phần AI/Audio |
| [AI_DECISIONS.md](AI_DECISIONS.md) | Quyết định kỹ thuật đã chốt (LLM = Groq, TTS, geofence) |
| [AI_SERVICE_MIGRATION.md](AI_SERVICE_MIGRATION.md) | Vị trí code sau khi chuyển sang Service layer |
| [AI_KEYS_AND_LOCAL_TEST.md](AI_KEYS_AND_LOCAL_TEST.md) | Lấy và cấu hình key an toàn, chạy thử local |
| [P4_AI_BOOKING_ASSISTANT.md](P4_AI_BOOKING_ASSISTANT.md) | Đặc tả AI Booking Assistant |
| [P3_BE_02_GEOFENCE.md](P3_BE_02_GEOFENCE.md) | Đặc tả geofence POI |
| [P3_BE_03_POI_AZURE_TTS.md](P3_BE_03_POI_AZURE_TTS.md) | Đặc tả POI + Azure TTS |
| [AUDIO_TRIGGER_DECISIONS.md](AUDIO_TRIGGER_DECISIONS.md) | Quy ước trigger audio |
| [TTS_LIVE_SMOKE_TEST.md](TTS_LIVE_SMOKE_TEST.md) | Smoke test TTS với dịch vụ thật |
| [INTEGRATION_LATER.md](INTEGRATION_LATER.md) | Việc tích hợp với module nhóm khác |

Liên quan: [`../SIGHTSEEING_AUDIO_PLAYBACK.md`](../SIGHTSEEING_AUDIO_PLAYBACK.md), [`../DEMO_AUDIO.md`](../DEMO_AUDIO.md).
