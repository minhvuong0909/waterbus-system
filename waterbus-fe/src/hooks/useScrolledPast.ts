import { useEffect, useState } from 'react'

/** True khi trang đã cuộn quá `offset` px; chỉ cập nhật khi giá trị đổi nên không làm vẽ lại mỗi lần cuộn. */
export function useScrolledPast(offset: number): boolean {
  const [past, setPast] = useState(() => window.scrollY > offset)

  useEffect(() => {
    const onScroll = () => setPast(window.scrollY > offset)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [offset])

  return past
}
