import type { ReactNode } from 'react'
import HoldCountdown from './HoldCountdown'

/**
 * Đồng hồ giữ ghế đặt ở đầu khung tóm tắt dính, để cuộn tới đâu cũng thấy còn bao lâu. Chỉ hiện khi
 * khung đang dính (từ màn hình lg); ở màn nhỏ, đồng hồ nằm ở đầu trang như trước.
 */
export default function SummaryTimer({ fallback = null }: { fallback?: ReactNode }) {
  return (
    <div className="mb-space-md hidden lg:block [&>span]:flex [&>span]:w-full [&>span]:justify-center">
      <HoldCountdown fallback={fallback} />
    </div>
  )
}
