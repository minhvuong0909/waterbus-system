# 🚤 Saigon Smart Waterbus System

Hệ thống đặt vé tàu buýt & du thuyền đường sông Sài Gòn thông minh (PRN232 / Advanced Software Project). Nền tảng kết hợp thương mại điện tử đặt vé theo đoạn, điều phối thời gian thực (Real-time tracking), trợ lý AI thông minh và ứng dụng soát vé ngoại tuyến cho nhân viên bến.

---

## 📌 Tổng Quan Dự Án (Project Overview)

**Smart Waterbus** giải quyết bài toán đặt chỗ tàu buýt đường sông đô thị theo chặng (Segment-based seat reservation) với độ trễ thấp, chống đặt trùng ghế 2 lớp (Redis Distributed Lock + EF Core RowVersion concurrency) và phát vé điện tử ký số bảo mật.

### Các Phân Hệ Cốt Lõi:
1. **Khách hàng / Vãng lai (Customer / Guest Portal)**: Tra cứu chuyến, bản đồ tuyến sông, chọn ghế trực quan theo đoạn, thanh toán VNPay, ví vé điện tử QR động.
2. **Quản trị & Điều hành (Admin & Captain Portal)**: Giám sát radar vị trí đội tàu thời gian thực, quản lý sự cố thời tiết, cập nhật trạng thái chuyến.
3. **Soát vé di động (Staff Scanner App)**: Ứng dụng di động chuyên dụng cho nhân viên bến, quét và xác thực QR chữ ký số hoàn toàn offline, đồng bộ lô (Batch sync) chống trùng lặp.
4. **Hệ thống AI & Thuyết minh thông minh**: Trợ lý AI tư vấn tìm chuyến (Semantic Kernel), định vị geofence tự động kích hoạt audio thuyết minh địa danh (POI) dọc sông Sài Gòn.

---

## 🏗️ Kiến Trúc Hệ Thống (Architecture & Structure)

```text
WaterbusSystem/
├── waterbus-be/             # Backend .NET 8 Web API (Clean Architecture)
│   ├── src/
│   │   ├── WaterbusSystem.Domain/          # Entities, Enums, Domain Rules, Exceptions
│   │   ├── WaterbusSystem.Application/     # CQRS Feature Slices (MediatR), Validators, DTOs
│   │   ├── WaterbusSystem.Infrastructure/  # EF Core, SQL Server, Redis, VNPay, SignalR, AI
│   │   └── WaterbusSystem.WebApi/          # Controllers, Hubs, Middlewares, Program.cs
│   └── tests/
│       └── WaterbusSystem.UnitTests/       # 450+ Unit & Integration Tests (Domain, Commercial, AI)
│
├── waterbus-fe/             # Web Frontend (React 18 + Vite + TypeScript + TailwindCSS)
│   ├── src/
│   │   ├── features/        # Booking, Trips, Tickets, Tracking, Explore, Payment
│   │   ├── components/      # UI Design System, Haptic Buttons, Modals, Navbars
│   │   ├── store/           # Global state management
│   │   └── constant/        # API Endpoints, Configuration, Themes
│   └── public/              # Static assets, Maps, Icons
│
├── waterbus-mobile/         # Mobile App cho Nhân viên soát vé (Expo SDK 57 + React Native)
│   └── myApp/
│       ├── app/             # Expo Router (File-based routing: Tabs, Scanner, Settings)
│       ├── src/services/    # SQLite Local Database, Offline QR Verifier, Sync Worker
│       └── scripts/         # Automated Scanner Verification Suite (13/13 tests)
│
├── docs/                    # Tài liệu chuẩn nghiệp vụ & Hợp đồng API
│   ├── Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md  # Nguồn chuẩn nghiệp vụ (BR v2.0)
│   ├── CONVENTIONS.md       # Quy ước chuẩn kiến trúc & mã nguồn
│   ├── 02_Logical_ERD_REVIEWED.dbml                           # Thiết kế CSDL chi tiết (33 bảng)
│   ├── REALTIME_CONTRACT.md                                    # Hợp đồng SignalR Hubs
│   ├── TRIP_READ_API.md                                        # Hợp đồng API Trip & Seat Map
│   ├── NHAT_ANH_COMMERCIAL_API.md                              # Hợp đồng Checkout, Payment, Refund
│   └── thinh-ai/                                               # Tài liệu triển khai AI & Audio TTS
│
├── AGENTS.md                # Quy tắc chỉ dẫn bắt buộc cho AI Coding Agents
└── docker-compose.yml       # Khởi chạy toàn bộ hệ thống (API, Web, SQL Server, Redis)
```

---

## 👥 Vai Trò & Phân Công (Role Assignments)

Hệ thống được thiết kế theo 5 actor nghiệp vụ chuẩn (**Passenger, Guest, Admin, Staff, Captain** - *không có Dispatcher*):

| Thành viên | Phân hệ phụ trách | Trọng tâm kỹ thuật |
|---|---|---|
| **Vương** | **Team Leader & Core Backend** | Quản lý dự án, chuẩn hóa nghiệp vụ BR v2.0, API Tra cứu chuyến & Sơ đồ ghế theo đoạn. |
| **Nhật Anh** | **Commercial Backend** | Luồng giao dịch thương mại: Checkout, Giữ chỗ Redis, Thanh toán VNPay, Hoàn tiền tự động/thủ công, Ký số QR RSA. |
| **Kiệt** | **Real-time, Background Jobs & DevOps** | SignalR Hubs (`/hubs/seat-sync`, `/hubs/boat-tracking`), Hangfire Weather Guard, Docker CI/CD, Nginx Gateway. |
| **Viễn** | **Web Frontend Portal** | Giao diện Web đặt vé, Live GPS Radar bản đồ sông, Audio player du lịch, Quản trị Admin & Captain. |
| **Tuấn** | **Staff Scanner Mobile** | Ứng dụng Expo Mobile: Camera scanner, Xác thực QR chữ ký số offline, SQLite local cache & Batch sync worker. |
| **Thịnh** | **AI & Sightseeing Audio** | Trợ lý ảo tìm chuyến Semantic Kernel, Phân tích Geofence POI trắc địa (NetTopologySuite), Azure AI Speech TTS & Blob Storage. |

---

## 🚀 Khởi Chạy Nhanh (Quick Start)

### 1. Khởi chạy bằng Docker Compose (Khuyến nghị)
Hệ thống hỗ trợ chạy trọn gói gồm SQL Server 2022, Redis 7, Backend API và Frontend Nginx:

```bash
# 1. Sao chép file cấu hình môi trường mẫu
cp .env.example .env

# 2. Khởi chạy toàn bộ dịch vụ
docker compose up -d --build
```
* **Frontend Web**: [http://localhost:8080](http://localhost:8080)
* **Backend API Swagger**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
* **Hangfire Dashboard (Admin)**: [http://localhost:5000/hangfire](http://localhost:5000/hangfire)

---

### 2. Khởi chạy từng phân hệ Local (Development Mode)

#### A. Backend (.NET 8):
```bash
cd waterbus-be
dotnet restore WaterbusSystem.sln
dotnet build WaterbusSystem.sln
dotnet test WaterbusSystem.sln                 # Chạy toàn bộ 450+ unit tests
dotnet run --project src/WaterbusSystem.WebApi # Chạy API server tại :5000
```

#### B. Frontend Web (React + Vite):
```bash
cd waterbus-fe
pnpm install
pnpm dev                                       # Mở http://localhost:5173
```

#### C. Mobile Scanner (Expo SDK 57):
```bash
cd waterbus-mobile/myApp
pnpm install
pnpm dev                                       # Chạy Metro Bundler tại :8081
pnpm run test:scanner                          # Chạy test suite soát vé offline (13/13 passed)
```

---

## 📜 Quy Ước Nghiệp Vụ & Mã Nguồn (Key Conventions)

- **Nguồn chân lý nghiệp vụ**: Tuân thủ chuẩn [`docs/Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md`](docs/Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md).
- **Quy tắc cho AI & Developers**: Bắt buộc đọc [`AGENTS.md`](AGENTS.md) và [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) trước khi chỉnh sửa mã nguồn.
- **Ghế giữ theo đoạn (`VisitOrder`)**: Không dùng `Station.OrderIndex`. Kiểm tra chồng lấn đoạn trong transaction.
- **Phát vé an toàn**: Vé (`Ticket`) chỉ phát sinh sau khi thanh toán thành công (`Ticket.Issue()`). Không phát hành vé sớm khi thanh toán chưa hoàn tất.
- **Bảo mật**: Không commit secrets (`.env`, private keys, payment credentials) vào kho mã nguồn.
