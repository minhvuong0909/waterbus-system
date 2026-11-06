# Email thông báo giao dịch (Transactional Outbox) — Kế hoạch & quyết định triển khai

Nhánh: `feature/email-notifications` (từ `origin/main` @ `ed745d0`). PR #57/#58 (mobile/check-in) đang mở: PR này **không** sửa CheckIns/Mobile.
Nguồn chuẩn: `AGENTS.md`, `docs/CONVENTIONS.md`, BR v2.0. Kiến trúc: **Service + Repository + Unit of Work**, không MediatR.

## 1. Hiện trạng đã khảo sát (đúng với code, không phải suy đoán)

| Chủ đề | Thực tế |
|---|---|
| Xác nhận thanh toán | `PaymentConfirmationService.ProcessAsync` chạy trong `ICommercialTransactionRunner` (SQL transaction + `sp_getapplock waterbus:commercial`). Nhánh thành công: `Order.Status=Confirmed`, `Booking.Status=Confirmed`, `Ticket.Issue(...)`, rồi **1** `SaveChanges` → commit. IPN lặp lại trả `02` **trước** khi tới nhánh ghi (payment đã `Succeeded`). |
| Thanh toán cần đối soát | Nhánh `ReconciliationRequired` (muộn/hết hạn/trip không còn bán): Order → `Expired`, **không** có Ticket. Không phải "đã xác nhận". |
| Trip | Chỉ `TripService.ChangeStatusAsync` đổi `Trip.Status` (đã grep toàn solution). `Cancel` (chưa khởi hành) ≠ `Terminate` (đang chạy) theo `TripLifecycle`; cả hai là căn cứ hoàn tiền (`RefundEligibilityEvaluator`). Mỗi lần đổi ghi `TripOperationEvent`. Lặp lại thao tác → `ConflictException` (NextStatus = null). |
| Hoàn tiền | 3 đường tới `FinancialTransaction.Status = Succeeded`: (1) `RefundRequestService.ApplyGatewayOutcome` (VNPAY trả ngay), (2) `RefundReconciliationService` (truy vấn VNPAY, `Processing → Succeeded`), (3) `ManualRefundService` (Admin xác nhận). Timeout gateway → `Processing` (không phải thành công). Endpoint hoàn tiền là **AdminOnly** → khách không tự yêu cầu hoàn; "bước tiếp theo" cho khách = liên hệ hỗ trợ. |
| Người nhận | `PurchaseOrder.PurchaserEmail/PurchaserName`. Email hành khách (`Booking.CustomerEmail`, tuỳ chọn) **không** dùng. |
| QR | Per-Booking, ký RSA (`IBookingQrSigner.Sign(publicBookingId, version, tripId, issuedAt)`); `BookingQrService` yêu cầu `ManageOrder` AccessGrant. Token Guest chỉ lưu **hash**, raw token chỉ trả 1 lần ở checkout và FE giữ trong trình duyệt → **không có link xuyên thiết bị an toàn hiện hữu**. |
| Hangfire | SQL Server storage, 2 worker, recurring job đăng ký trong `UseBackgroundJobs()`. |
| Test hạ tầng | `WATERBUS_TEST_SQLSERVER`; fixture tự tạo/xoá DB `WaterbusDomainTests_*` (`CommercialSqlTests`). CI có SQL Server service. |
| FE | `ConfirmationHero.tsx`/`ManageLinkCard.tsx` ghi rõ "backend chưa gửi email". |

## 2. Trigger → nguồn sự thật (committed transition)

| Email | Trigger (cùng transaction với thay đổi nghiệp vụ) | Khoá idempotency (UNIQUE) |
|---|---|---|
| EMAIL-01 `BookingConfirmed` | `PaymentConfirmationService` — **chỉ** nhánh `else` (Order→Confirmed + Ticket đã phát) | `BOOKING_CONFIRMED:{orderId}` |
| EMAIL-02 `TripCancelled` | `TripService.ChangeStatusAsync` khi `next == Cancelled`: mỗi Booking `Confirmed` có Ticket của Trip | `TRIP_CANCELLED:{bookingId}` |
| EMAIL-02 `TripTerminated` | như trên khi `next == Terminated` (nội dung khác: dừng giữa hành trình) | `TRIP_TERMINATED:{bookingId}` |
| EMAIL-03 `RefundSucceeded` | 3 đường `Succeeded` ở trên | `REFUND_SUCCEEDED:{refundTransactionId}` |

*Cancelled* và *Terminated* là **hai loại riêng** (quyền lợi/ngữ cảnh khác nhau), không gộp.
Một Order khứ hồi (2 Trip) → 1 email xác nhận liệt kê 2 chặng; huỷ 1 Trip chỉ email cho đúng Booking của Trip đó. Một Booking nhiều Ticket → 1 email, mỗi Ticket 1 dòng hành khách.

## 3. Kiến trúc

```
WebApi ──► Application ──► Domain          Infrastructure ──► Application ──► Domain
```
**Domain**: `EmailOutboxMessage` (entity), `EmailNotificationType`, `EmailOutboxStatus`.
**Application** (`Features/Notifications`): giao diện + use case, không có SMTP/EF/Hangfire/MailKit.
- `INotificationOutbox` / `NotificationOutbox` — ranh giới hẹp duy nhất mà các service nghiệp vụ gọi: `EnqueueBookingConfirmedAsync(orderId)`, `EnqueueTripDisruptionAsync(tripId, type)`, `EnqueueRefundSucceededAsync(refundId)`. Chỉ `Add` vào DbContext hiện tại; **không** `SaveChanges` (caller commit cùng transaction nghiệp vụ). Chỉ lưu **tham chiếu** (id) + người nhận, **không** dựng nội dung trong transaction thanh toán ⇒ lỗi template không thể làm hỏng thanh toán.
- `IEmailOutboxRepository` (claim/lease/đánh dấu kết quả), `INotificationDataRepository` (read-model chiếu từ DB cho nội dung).
- `NotificationEmailComposer` + `Templates/*` — dựng HTML + text từ dữ liệu **đã commit**, kiểm tra điều kiện còn đúng lúc gửi (vd Order còn `Confirmed`, refund còn `Succeeded`), HTML-encode mọi trường do khách nhập.
- `EmailOutboxProcessor` — claim → compose → send → ghi kết quả; backoff luỹ thừa có chặn; dead-letter.
- `IEmailTransport`, `IQrImageRenderer` — cổng ra ngoài.
**Infrastructure**: `EmailOutboxRepository`/`NotificationDataRepository` (EF), `MailKitEmailTransport`, `QrCoderImageRenderer`, `EmailOutboxJob` (Hangfire), `EmailOptionsValidator`, EF configuration + migration `AddEmailOutbox`.
**WebApi**: đăng ký recurring job `email-outbox` trong `UseBackgroundJobs()`.

### Sequence

```mermaid
sequenceDiagram
    autonumber
    participant V as VNPay
    participant API as PaymentConfirmationService
    participant DB as SQL Server
    participant HF as Hangfire (email-outbox, mỗi phút)
    participant P as EmailOutboxProcessor
    participant S as SMTP (MailKit)
    V->>API: IPN (HMAC)
    API->>API: Verify chữ ký + số tiền + trạng thái Order
    API->>DB: BEGIN + applock
    API->>DB: Order=Confirmed, Ticket.Issue, INSERT EmailOutbox(Pending, EventKey UNIQUE)
    API->>DB: COMMIT (cùng 1 transaction)
    API-->>V: RspCode 00 (không chờ email)
    loop mỗi phút
        HF->>P: ProcessBatch
        P->>DB: Claim: UPDATE…READPAST → Processing + LockToken + LockedUntil
        P->>DB: Đọc dữ liệu đã commit (Order/Booking/Ticket/Refund)
        P->>P: Kiểm tra điều kiện + render HTML/text + QR (ký tại chỗ)
        P->>S: Send (Message-Id ổn định theo outbox id)
        alt chấp nhận
            P->>DB: Sent + ProviderMessageId (guard LockToken)
        else lỗi tạm thời
            P->>DB: Failed + NextAttemptAt (backoff); hết MaxAttempts → DeadLetter
        else bị từ chối vĩnh viễn
            P->>DB: DeadLetter
        end
    end
    Note over P,DB: Worker chết → LockedUntil quá hạn → lần quét sau claim lại
```

## 4. Outbox: schema & bảo đảm

`EmailOutbox`: `Id`, `EventKey` (**UNIQUE**, nvarchar(200)), `NotificationType`, `OrderId`, `BookingId?`, `FinancialTransactionId?`, `RecipientEmail`, `RecipientName`, `Status`, `AttemptCount`, `NextAttemptAt`, `LockedUntil`, `LockToken`, `SentAt`, `ProviderMessageId`, `LastError` (đã làm sạch, ≤500), + `BaseEntity` (RowVersion…). Index: `UX_EmailOutbox_EventKey`; `IX_EmailOutbox_Dispatch (Status, NextAttemptAt)` ; `IX_EmailOutbox_OrderId`.

- **Cùng transaction**: INSERT chung `DbContext` với thay đổi nghiệp vụ ⇒ rollback thì không có email; commit thì bền qua restart.
- **Idempotency**: unique index DB là chốt chặn cuối; thêm pre-check `ExistsByEventKey` để tránh ném lỗi ở đường bình thường. Các đường ghi tiền đều giữ applock thương mại ⇒ không có race tạo trùng; nếu có bug thì unique index **fail-closed** (rollback) chứ không tạo trùng.
- **Claim nguyên tử** (nhiều worker): `UPDATE TOP(n) … WITH (UPDLOCK, READPAST, ROWLOCK)` đặt `Processing + LockToken + LockedUntil`, `AttemptCount+1`. Ghi kết quả có điều kiện `LockToken` ⇒ worker "zombie" không ghi đè.
- **Phục hồi crash**: bản ghi `Processing` có `LockedUntil < now` được claim lại; nếu đã đạt `MaxAttempts` thì chuyển `DeadLetter` thay vì lặp vô hạn.
- **Không phụ thuộc enqueue sau commit**: polling bền vững; độ trễ ≤ chu kỳ cron (mặc định 1 phút).
- **Bản ghi cũ**: không bao giờ tự bị bỏ qua vì tuổi — xem §15 (chính sách theo loại; quá hạn ⇒ `DeadLetter` "StaleNeedsReview" chờ vận hành).
- **Email tắt** (`Email:Enabled=false`, mặc định): job không claim ⇒ bản ghi nằm `Pending`, **không bao giờ** bị đánh dấu `Sent`.
- **Người nhận không hợp lệ**: vẫn ghi 1 dòng `DeadLetter` ("InvalidRecipient") để vận hành thấy — không mất thông báo âm thầm.

### Ngữ nghĩa giao nhận (trung thực)
DB bảo đảm **1 thông báo logic / sự kiện**. SMTP không có exactly-once: nếu server nhận thư rồi timeout trước khi ta ghi `Sent`, lần retry gửi **bản thứ hai vật lý**. Giảm thiểu: `Message-Id` ổn định (`<outboxId@domain>`) + header `X-Entity-Ref-ID` để provider/MTA/MUA khử trùng khi hỗ trợ; backoff dài; lease dài hơn timeout. Chấp nhận rủi ro còn lại (ghi trong §Giới hạn).

## 5. Provider
Chưa có provider nào trong repo ⇒ thêm **MailKit** (SMTP, async, TLS) sau `IEmailTransport` (thay được bằng Postmark/Resend/SendGrid mà không đụng nghiệp vụ). Cấu hình `Email:*`: `Enabled`, `Smtp:Host/Port/Security(StartTls|SslOnConnect|None)/Username/Password/TimeoutSeconds`, `AllowInsecureTransport` (chỉ ngoài Production), `FromAddress/FromName`, `PublicBaseUrl`, `SupportEmail/SupportPhone` (tuỳ chọn, chỉ hiển thị khi có), `MaxAttempts/BatchSize/LeaseMinutes/MaxMessageAgeHours/Cron`. **Fail-fast khi khởi động** nếu `Enabled=true` mà cấu hình sai (host/port/From/BaseUrl, `Security=None` ở Production, thiếu mật khẩu khi có user). Không fallback ngầm sang kênh không an toàn. Dev: Mailpit (docker compose) hoặc SMTP catcher cục bộ.

## 6. QR & truy cập vé (quyết định bảo mật)
- Không tạo token/link Guest mới (tránh bearer token dài hạn trong email & lưu raw token).
- Email **nhúng ảnh QR** (CID inline) của chính credential đã ký hiện có (`IBookingQrSigner.Sign` với dữ liệu Booking đã commit; **ký tại thời điểm gửi**, không lưu QR trong outbox). Cùng mức phơi bày với trang vé hiện tại; email ghi rõ "không chia sẻ".
- Email **không** chứa link quản lý đơn/`ManageOrder` token; "My Tickets" chỉ khả dụng trên thiết bị đã đặt (nói thẳng trong email, không khẳng định "mở được mọi thiết bị").
- Giới hạn đã biết: ai đọc được hộp thư người mua có thể dùng QR (tương tự mọi hệ thống vé email).

## 7. Bảo mật & quyền riêng tư
HTML-encode toàn bộ dữ liệu người dùng; header chống CRLF (MimeKit + validate địa chỉ); không log body/QR/credential/email đầy đủ (log: outbox id, type, status, mask email); `LastError` làm sạch (không chứa mật khẩu/địa chỉ); không endpoint công khai mới; không resend endpoint (vận hành qua SQL — xem runbook).

## 8. Kiểm thử (xem `waterbus-be/tests/WaterbusSystem.UnitTests/Notifications/`)
Unit (InMemory) cho enqueue/template/processor; **SQL Server thật** cho: unique index, rollback, claim đồng thời, phục hồi crash, trùng sự kiện song song; test MailKit với SMTP catcher TCP cục bộ; test validator cấu hình. Không khẳng định kiểm thử đồng thời bằng InMemory.

## 9. Triển khai
Biến môi trường (`Email__*`), migration `AddEmailOutbox` (bảng mới, **không** phá dữ liệu), `docker-compose` thêm Mailpit cho dev, CI chạy bộ test Notifications trong job backend hiện có (có SQL Server).

## 10. Rà soát phản biện (trước khi code)
| Rủi ro | Xử lý |
|---|---|
| SMTP lỗi làm hỏng thanh toán | Không gọi SMTP trong transaction; enqueue chỉ INSERT 1 dòng nhỏ |
| Dựng nội dung trong transaction → lỗi bắn ngược | Chỉ lưu tham chiếu; compose ở worker |
| Trip huỷ đồng thời với IPN xác nhận | `TripService` cancel/terminate chạy trong cùng `ICommercialTransactionRunner` (cùng applock) ⇒ tuần tự hoá; IPN vốn kiểm tra trạng thái Trip |
| Worker chết giữa chừng | Lease + reclaim + giới hạn attempt |
| Gửi thư cũ sau khi bật SMTP | `MaxMessageAgeHours` → `Skipped` |
| Compose sau khi trạng thái đổi | Compose kiểm tra lại (vd refund không còn `Succeeded` → `Skipped`) |
| Vi phạm kiến trúc | Application chỉ có interface; Infrastructure giữ MailKit/EF/Hangfire/QRCoder |

---

# Phần B — Quyết định thực tế sau khi triển khai

## 11. Runbook vận hành (inspect / retry / dead-letter)

Trạng thái: `Pending` (chờ) · `Processing` (worker đang giữ lease) · `Sent` · `Failed` (lỗi tạm, sẽ retry ở `NextAttemptAt`) · `DeadLetter` (dừng, cần người xử lý) · `Skipped` (cố ý không gửi vì dữ liệu/trạng thái không còn đúng; `LastError` ghi lý do — KHÔNG dùng cho lý do tuổi).

```sql
-- Tổng quan
SELECT Status, COUNT(*) AS N FROM EmailOutbox GROUP BY Status;
-- Đang chờ / đang lỗi tạm
SELECT Id, NotificationType, Status, AttemptCount, NextAttemptAt, LastError, CreatedAt
FROM EmailOutbox WHERE Status IN ('Pending','Failed') ORDER BY NextAttemptAt;
-- Dead-letter & Skipped (LastError cho biết lý do; KHÔNG chứa địa chỉ email/nội dung)
SELECT Id, EventKey, NotificationType, AttemptCount, LastError, UpdatedAt
FROM EmailOutbox WHERE Status IN ('DeadLetter','Skipped') ORDER BY UpdatedAt DESC;
-- Processing bị kẹt (worker chết): tự được claim lại sau LockedUntil; chỉ để quan sát
SELECT Id, LockedUntil, AttemptCount FROM EmailOutbox WHERE Status='Processing' AND LockedUntil < SYSDATETIMEOFFSET();
```

**Retry an toàn một bản ghi `DeadLetter`** (sau khi đã sửa nguyên nhân: SMTP, cấu hình, địa chỉ nhận sai…):
1. Xác nhận nguyên nhân từ `LastError`. Nếu là `InvalidRecipient`: sửa `PurchaseOrders.PurchaserEmail` **và** `EmailOutbox.RecipientEmail` (người nhận được chụp tại thời điểm enqueue).
2. Đặt lại đúng một dòng (không đổi `EventKey` ⇒ vẫn là một thông báo logic):
```sql
UPDATE EmailOutbox SET Status='Pending', AttemptCount=0, NextAttemptAt=SYSDATETIMEOFFSET(),
       LockToken=NULL, LockedUntil=NULL, LastError=NULL, UpdatedAt=SYSDATETIMEOFFSET()
WHERE Id = @Id AND Status IN ('DeadLetter','Failed');   -- KHÔNG áp dụng cho Sent/Processing
```
3. Bản ghi `DeadLetter` với `LastError` bắt đầu bằng `StaleNeedsReview` là chưa gửi được quá hạn chính sách (§15). **Người vận hành quyết định**: còn đáng gửi không (xem Order/Trip/Refund hiện tại)? Nếu có, đặt `CreatedAt = SYSDATETIMEOFFSET()` cùng lệnh UPDATE ở bước 2 để cửa sổ tuổi bắt đầu lại; nếu không, để nguyên `DeadLetter` và ghi chú lý do vào ticket. Bản ghi tài chính (xác nhận/hoàn tiền) cần xem xét trong vòng 1 ngày làm việc sau khi xuất hiện.
4. Worker chạy mỗi phút; theo dõi log `Email outbox … sent` hoặc cột `Status`.
**Không** `INSERT` thủ công, **không** xoá dòng `Sent` (mất bằng chứng + mở cửa gửi trùng), **không** retry hàng loạt khi chưa biết nguyên nhân.

Hangfire dashboard (`/hangfire`, Admin/Basic Auth) hiển thị job recurring `email-outbox`. Bật email lần đầu: các bản ghi `Pending` tích luỹ khi email tắt sẽ được gửi nếu còn trong cửa sổ chính sách; bản ghi quá cửa sổ chuyển `DeadLetter` (StaleNeedsReview) để vận hành xem xét, không bị mất.

## 12. Cấu hình & triển khai

| Biến môi trường | Mặc định | Ghi chú |
|---|---|---|
| `Email__Enabled` | `false` | `false`: outbox vẫn ghi nhưng **không gửi, không đánh dấu Sent** |
| `Email__FromAddress` / `Email__FromName` | – / `Smart Waterbus` | Bắt buộc khi bật |
| `Email__PublicBaseUrl` | – | https bắt buộc ở Production; dùng cho link "Vé của tôi" |
| `Email__SupportEmail` / `Email__SupportPhone` | trống | Chỉ hiển thị khi điền; để trống thì email không in kênh liên hệ nào |
| `Email__Smtp__Host/Port/Security` | –/587/`StartTls` | `StartTls`\|`SslOnConnect`\|`None` |
| `Email__Smtp__Username/Password` | trống | Phải cùng có hoặc cùng trống; chỉ để ở user-secrets/secret store |
| `Email__Smtp__AllowInsecureTransport` | `false` | Cần cho `Security=None`; **luôn bị từ chối ở Production** |
| `Email__Smtp__TimeoutSeconds` | 30 | `LeaseMinutes*60` phải > 4× giá trị này (connect + auth + send + disconnect) |
| `Email__MaxAttempts` / `BatchSize` / `LeaseMinutes` / `MaxMessageAgeHours` / `Cron` | 8 / 20 / 10 / 48 / `* * * * *` | `MaxAttempts`, `MaxMessageAgeHours`: thông báo vận hành (huỷ/chấm dứt chuyến) |
| `Email__FinancialMaxAttempts` / `FinancialMaxAgeHours` | 16 / 168 | Thông báo tài chính (xác nhận vé, hoàn tiền); `FinancialMaxAgeHours` ≥ `MaxMessageAgeHours` |

Dịch vụ ngoài cần có: SMTP relay (SES/SendGrid/Mailgun SMTP/…); SPF/DKIM/DMARC cho domain gửi (ngoài phạm vi code). Dev: `docker compose --profile mail-dev up -d mailpit` rồi đặt `EMAIL_SMTP_HOST=mailpit EMAIL_SMTP_PORT=1025 EMAIL_SMTP_SECURITY=None EMAIL_SMTP_ALLOW_INSECURE=true ASPNETCORE_ENVIRONMENT=Development`, xem thư ở http://localhost:8025.
Migration `AddEmailOutbox` chỉ **tạo bảng mới** (không đụng dữ liệu cũ); rollback = `ef migrations remove`/drop bảng, ứng dụng cũ không phụ thuộc bảng này.

## 13. Khác biệt so với kế hoạch ban đầu / lưu ý thiết kế

- Đã dùng **tham chiếu** thay cho snapshot JSON: outbox chỉ giữ id + người nhận; nội dung dựng lúc gửi từ dữ liệu đã commit (Order/Booking/Ticket/Refund là bất biến sau khi phát hành) và **kiểm tra lại điều kiện** — giảm rủi ro làm hỏng transaction thanh toán và tránh nhân đôi PII trong DB.
- `TripService.ChangeStatusAsync` nay nhận thêm `INotificationOutbox` + `ICommercialTransactionRunner`; **chỉ nhánh Cancel/Terminate** chạy trong applock thương mại (các thao tác khác giữ nguyên đường cũ).
- Người nhận được chụp lúc enqueue từ `PurchaseOrder.PurchaserEmail`; email hành khách không bao giờ dùng.
- Không có endpoint resend/inspect công khai (tránh mass-email/enumeration); vận hành qua SQL (§11).
- Mẫu render: `docs/email-samples/*.html|*.txt` (sinh bằng `WATERBUS_WRITE_EMAIL_SAMPLES=<thư mục> dotnet test --filter RendersExampleSamples`; ảnh QR là `cid:` nên không hiện khi mở trực tiếp file).

## 14. Hạn chế đã biết (trung thực)

1. **Không có exactly-once vật lý.** DB bảo đảm một thông báo logic/sự kiện. Nếu SMTP server đã nhận thư nhưng ta timeout/crash trước khi ghi `Sent`, lần retry gửi **bản thứ hai** (cùng `Message-Id`, một số MTA/MUA sẽ khử trùng, không bảo đảm).
2. **Độ trễ ≤ 1 phút** (chu kỳ cron) — không enqueue Hangfire tức thì sau commit (chủ ý để không phụ thuộc vào nó).
3. **QR trong email là bearer credential**: ai đọc được hộp thư người mua dùng được QR (cùng mức với trang vé hiện tại). Email không chứa link/token quản lý đơn nên **không có link xuyên thiết bị**; khách xem lại vé bằng chính email (QR) hoặc link quản lý lưu từ lúc đặt vé.
4. FE không biết `Email:Enabled`, nên câu chữ trang xác nhận mang tính điều kiện ("nếu dịch vụ email được bật, email xác nhận có thể được gửi…") và nhấn mạnh vé có ngay, không cần email để lên tàu — đúng với mọi cấu hình.
5. Chưa kiểm chứng với **nhà cung cấp SMTP thật** (không có credential): đã kiểm bằng MailKit → SMTP catcher cục bộ (trong test) và một API chạy thật + SQL Server + catcher.
6. Hoàn tiền: chỉ thông báo khi `Succeeded`. Hoàn tiền `LATE_REFUND_*` (thanh toán muộn, không có vé) cũng được thông báo (Order `Expired`, không có Ticket) với nội dung riêng.
7. Nhắc giờ khởi hành (EMAIL-04) chưa làm: thêm `EmailNotificationType` + khoá + template + một nhánh `Enqueue…` từ job quét.

## 15. Sửa theo review PR #59 (F01–F04 + CI)

**F01 — chính sách trạng thái giữa lúc thanh toán và lúc gửi.** Composer đánh giá lại *từng chặng* (Booking) lúc gửi: chặng "còn hiệu lực" ⇔ Booking `Confirmed/Completed` **và** Trip không `Cancelled/Terminated/Completed`.
| Tình huống lúc gửi | Kết quả |
|---|---|
| Mọi chặng còn hiệu lực | Email xác nhận đầy đủ, 1 QR/chặng |
| Một số chặng hết hiệu lực (khứ hồi bị huỷ 1 chiều) | Xác nhận chỉ mô tả chặng còn hiệu lực, **không** kèm QR chặng kia, có ghi chú "đặt chỗ X không còn hiệu lực — nhận thông báo riêng" |
| Không chặng nào còn hiệu lực (huỷ/chấm dứt/hoàn tất trước khi gửi, hoặc đã hoàn tiền) | `Skipped` (lý do trong `LastError`); khách nhận thông báo huỷ/chấm dứt thay thế |
| Email huỷ/chấm dứt | Chỉ phụ thuộc trạng thái Trip (vẫn gửi được sau khi hoàn tiền đổi trạng thái Booking) |
| Email hoàn tiền | Chỉ phụ thuộc refund `Succeeded` |
`Skipped` chỉ dùng cho lý do trạng thái, không bao giờ cho lý do tuổi.

**F02 — lease theo từng bản ghi.** Worker claim **một bản ghi mỗi lần** (lặp tối đa `BatchSize` lần), nên lease `LeaseMinutes` bắt đầu đúng lúc xử lý bản ghi đó; bản ghi cuối của lô không thể hết lease khi chờ các bản ghi trước. Validator yêu cầu `LeaseMinutes*60 > 4×Smtp:TimeoutSeconds`. Kết quả ghi vẫn có điều kiện `LockToken` (worker mất lease không ghi đè được).

**F03 — chính sách theo loại, không hủy âm thầm.**
| Loại | Số lần thử | Quá hạn |
|---|---|---|
| Tài chính (`BookingConfirmed`, `RefundSucceeded`) | `FinancialMaxAttempts` (16 ≈ vài ngày với backoff trần 6h) | sau `FinancialMaxAgeHours` (168h) ⇒ `DeadLetter` "StaleNeedsReview" + log Error |
| Vận hành (`TripCancelled/Terminated`) | `MaxAttempts` (8) | sau `MaxMessageAgeHours` (48h) ⇒ `DeadLetter` "StaleNeedsReview" |
Hết lượt thử cũng ⇒ `DeadLetter` (không `Skipped`). Mọi `DeadLetter` hiện trong truy vấn runbook §11 để người vận hành xem; chu trình xem xét tài chính ≤ 1 ngày làm việc.

**F04 — FE.** Câu chữ trang xác nhận mang tính điều kiện, đúng cả khi email tắt.

**CI.** Lỗi gốc: các class test SQL Server chạy song song (mỗi class tạo/migrate DB, mở hàng chục kết nối) làm SQL Server 2 core của CI quá tải ⇒ "pre-login handshake timeout" rồi runner bị huỷ. Sửa: mọi test dùng SQL thật (`CommercialSqlTests`, `DomainPersistenceTests`, `WeatherGuardConcurrencySqlTests`, `ReadinessCheckTests`, `EmailOutboxSqlTests`) thuộc collection `DisableParallelization` nên chạy tuần tự và không song song với test khác; giảm số tác vụ song song trong test của PR; bỏ bước CI chạy lặp lại bộ Notifications (đã nằm trong bước Test, được cổng "không bị skip" kiểm tra).
