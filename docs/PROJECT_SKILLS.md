# Skill UI của project

Hai bộ skill được cài cục bộ trong `.agents/skills/`, không phải dependency chạy trên web.

- Nguồn GSAP: https://github.com/greensock/gsap-skills — `gsap-core`, `gsap-frameworks`, `gsap-performance`, `gsap-plugins`, `gsap-react`, `gsap-scrolltrigger`, `gsap-timeline`, `gsap-utils`.
- Nguồn UI UX Pro Max: https://github.com/nextlevelbuilder/ui-ux-pro-max-skill — `ui-ux-pro-max` (gồm dữ liệu, tài liệu và script tìm kiếm).
- Cài ngày 2026-10-18 bằng helper `install-skill-from-github.py`, tham chiếu nhánh `main` tại thời điểm cài. Không tự cập nhật theo upstream.
- Giấy phép upstream được lưu trong `.agents/skills/GSAP-LICENSE.txt` và `.agents/skills/UI-UX-PRO-MAX-LICENSE.txt`.

## Cách dùng

Đọc `SKILL.md` tương ứng trước khi áp dụng. Skill không thay thế AGENTS.md hoặc quy tắc nghiệp vụ của dự án.

Tìm hướng dẫn UI bằng Python 3:

```powershell
python .agents/skills/ui-ux-pro-max/scripts/search.py "data table readability" --domain ux
python .agents/skills/ui-ux-pro-max/scripts/search.py "dashboard navigation" --stack react
```

## Áp dụng trong giao diện

- Admin: menu thu gọn trên điện thoại, Escape để đóng; có liên kết bỏ qua điều hướng cho bàn phím.
- Bảng: tiêu đề cột rõ, mã ở cột đầu không bị ngắt dòng; nút thao tác có chiều cao tối thiểu 44px.
- Tổng quan: `OverviewMotion.tsx` dùng GSAP + `useGSAP`, scope theo component, chỉ animate opacity/transform trong thời gian ngắn, không lặp lại khi đồng bộ dữ liệu; `matchMedia` tắt hiệu ứng theo `prefers-reduced-motion` và cleanup khi unmount.
- GSAP chỉ được import bởi trang Tổng quan tải qua route lazy; không thêm vào hiệu ứng cuộn chung của trang chủ. Không dùng ScrollTrigger hoặc vòng lặp theo dõi chuột cho admin.
- Chưa đo FPS trên thiết bị thật; không coi dung lượng bundle là phép đo tốc độ khung hình.

Phần web khách hàng cũng áp dụng các hướng dẫn responsive, phân cấp chữ và tương tác: header gọn trên điện thoại, menu đầy đủ từ breakpoint laptop, tóm tắt tìm chuyến ít cắt chữ, dải ngày có kích thước ổn định và video nền có nút dừng/phát. Các hiệu ứng cuộn đơn giản vẫn dùng API trình duyệt để giữ gói tải trang chủ nhỏ.
