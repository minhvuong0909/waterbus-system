# Quy trình branch, commit, kiểm thử và bàn giao

## 1. Trước khi code

1. `git status --short` và không đụng vào thay đổi của người khác.
2. Kiểm tra branch gốc của nhóm và đồng bộ theo quy trình nhóm. Không tự `pull`/merge khi working tree còn thay đổi chưa rõ owner.
3. Tạo một branch/workspace cho **một module được giao**. Các tên sau là ví dụ, không phải quy ước branch đã chốt của repo:
   - `feature/p3-be-03-azure-tts`
   - `feature/p3-be-02-poi-geofence`
   - `feature/p4-web-03-ai-assistant`
4. Đọc BR FINAL, roadmap module/dependency/DoD, Logical ERD, `SETUP.md`, conventions liên quan và source hiện tại; liệt kê file sẽ sửa.
5. Kiểm tra code hiện tại trước khi tạo abstraction/entity mới.

## 2. Quy tắc sửa code

- Một PR chỉ xử lý một module/submodule.
- Không format hàng loạt hoặc rename ngoài scope.
- Không commit generated `bin/`, `obj/`, audio, secret hoặc local config.
- Entity/schema change phải có lý do BR/contract, migration và cập nhật ERD nếu cần.
- Mọi behavior mới phải có test tương ứng.
- External provider phải có interface và fake/mock để test không cần Internet.
- Không xóa test hiện có để làm pipeline xanh.

## 3. Commit convention

Dùng Conventional Commits **như đề xuất cho phần việc này**; lịch sử repo có nhiều kiểu commit, nên xác nhận với nhóm nếu đã có chuẩn khác. Nội dung ngắn và có module ID trong scope:

```text
feat(p3-be-03): add text-to-speech abstraction
feat(p3-be-03): implement Azure Speech provider
feat(p3-be-02): add POI geofence distance service
test(p3-be-02): cover POI boundary and invalid coordinates
docs(thinh-ai): clarify audio storage decision
```

Không dùng commit chung chung như `update`, `fix code`, `done task` hoặc gộp CRUD + TTS + geofence + chatbot trong một commit.

Mỗi commit phải build được nếu hợp lý. Không commit secret rồi xóa ở commit sau vì secret vẫn nằm trong lịch sử Git.

**Khi nào commit:** commit sau khi hoàn thành một lát cắt nhỏ có thể review độc lập, đã chạy kiểm tra phù hợp và tự xem diff; làm trước khi chuyển sang lát cắt/branch khác hoặc mở PR. Không commit code đang hỏng, kết quả chỉ mới là nháp chưa kiểm tra, hoặc gom các module của Thịnh vào một commit lớn. Nếu bị chặn bởi contract nhóm, chỉ commit phần độc lập đã hoàn chỉnh và ghi rõ phần còn chờ trong PR; không đánh dấu DoD toàn module là xong.

**Cách commit:** stage đúng file thuộc lát cắt bằng đường dẫn tường minh, kiểm tra `git diff --cached --check`, `git diff --cached --stat` và `git diff --cached` trước `git commit`. Không dùng `git add .` khi workspace có thể chứa thay đổi của teammate. Commit message mô tả kết quả thực sự đã làm, không ghi `feat` nếu mới chỉ có tài liệu/contract.

## 4. Kiểm tra trước khi commit/PR

Với **commit có code backend**, chạy từ `waterbus-be`:

```powershell
dotnet restore WaterbusSystem.sln
dotnet build WaterbusSystem.sln --no-restore
dotnet test WaterbusSystem.sln --no-build
```

Sau đó:

```powershell
git status --short
git diff --check
git diff --stat
git diff
```

Với **commit chỉ sửa Markdown**, không cần chạy build/test backend nếu không đụng source; kiểm tra link nội bộ, mã BR được dẫn, nội dung/định dạng tài liệu và staged diff. Ghi rõ “docs-only, không chạy build/test” trong báo cáo commit/PR.

Tự kiểm tra:

- Có file ngoài scope/owner của thành viên khác không?
- Không có CRUD POI, ETA tới bến, BoatTrackingHub hoặc UI trong diff chứ?
- Dependency trong roadmap đã có và contract đã thống nhất chưa?
- Kết quả có đạt đúng DoD chính thức, không chỉ unit test/fake provider không?
- Có secret, URL chứa token hoặc connection string thật không?
- Endpoint mutate có `AdminOnly` chưa?
- Query có `AsNoTracking()` chưa?
- CancellationToken đã truyền tới EF/external call chưa?
- Provider lỗi có bị biến thành kết quả thành công không?
- Test có thật sự chứng minh rule hay chỉ chạy happy path?

## 5. Nội dung PR

```text
## Module và owner/dependency
P3-BE-xx

## Đã làm
- ...

## Business rules/contract áp dụng
- ...

## Không nằm trong PR
- ...

## Kiểm thử
- dotnet build ...: PASS/FAIL
- dotnet test ...: PASS/FAIL, N tests

## Cấu hình/migration
- ...

## Điểm cần reviewer xác nhận
- ...
```

## 6. Checklist giao việc cho Antigravity

- Giao đúng một lát cắt thuộc phần Thịnh, không giao nguyên ID roadmap `P3-BE-02`/`P3-BE-03` vì các dòng đó gộp ETA/CRUD POI ngoài scope.
- Cho agent đọc `README`, rules, module spec và workflow.
- Bắt agent báo kế hoạch/file scope trước khi sửa.
- Không chấp nhận agent tự thay BR hoặc mở rộng mobile/passenger audio.
- Bắt chạy build/test và cung cấp diff summary.
- Người thật review migration, authorization, provider config và secret trước khi merge.
- Nếu thay schema, tracking contract, Onboard Player contract hoặc UI contract, cần reviewer là owner phần tương ứng của nhóm.
