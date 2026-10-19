import { useEffect, useRef, useState } from 'react'

/** Chiều cao thanh header cố định phía trên trang (h-20 trên màn hình rộng). */
const HEADER_HEIGHT = 80

export interface PopoverPlacement {
  up: boolean
  maxHeight: number
}

/**
 * Logic chung của các ô chọn có danh sách thả xuống (bến, ngày): danh sách mở phía dưới nếu đủ chỗ,
 * không thì phía trên nếu đủ chỗ. Nếu cả hai đều chật, mở phía dưới và cuộn nhẹ trang để hiện đủ
 * danh sách (không bị cắt ở đáy màn hình, không chui dưới header cố định). Bấm ra ngoài hoặc nhấn
 * Esc thì đóng. `listHeight` là chiều cao ước tính của cả danh sách.
 */
export function usePopoverList(listHeight: number) {
  const root = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [placement, setPlacement] = useState<PopoverPlacement>({ up: false, maxHeight: 360 })

  const toggle = () => {
    if (!open && root.current) {
      const rect = root.current.getBoundingClientRect()
      const margin = 16
      const below = window.innerHeight - rect.bottom - margin
      const above = rect.top - HEADER_HEIGHT - margin
      const maxHeight = Math.min(listHeight, window.innerHeight - HEADER_HEIGHT - 2 * margin)
      if (below >= maxHeight) setPlacement({ up: false, maxHeight })
      else if (above >= maxHeight) setPlacement({ up: true, maxHeight })
      else {
        setPlacement({ up: false, maxHeight })
        window.scrollBy({ top: maxHeight - below, behavior: 'smooth' })
      }
    }
    setOpen((v) => !v)
  }

  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (root.current && !root.current.contains(event.target as Node)) setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return { root, open, setOpen, toggle, placement }
}
