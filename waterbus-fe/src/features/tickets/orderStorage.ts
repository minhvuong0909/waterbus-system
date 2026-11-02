import type { SavedOrder } from './types'

const STORAGE_KEY = 'waterbus.orders'
/** Giữ danh sách gọn; đơn cũ hơn vẫn mở được bằng link đã lưu. */
const MAX_ORDERS = 20

// Guest không có tài khoản nên "Vé của tôi" là các đơn đã mua trên thiết bị này. Lưu ở localStorage
// để đóng tab không mất vé; người dùng có thể xoá một đơn khỏi thiết bị (máy dùng chung).
// Storage có thể ném lỗi (chế độ riêng tư, chặn dữ liệu trang) nên mọi truy cập đều được bọc.

export function loadSavedOrders(): SavedOrder[] {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    const parsed: unknown = raw ? JSON.parse(raw) : []
    return Array.isArray(parsed)
      ? parsed.filter(
          (x): x is SavedOrder =>
            typeof x?.orderId === 'string' &&
            typeof x?.accessToken === 'string' &&
            typeof x?.savedAt === 'number',
        )
      : []
  } catch {
    return []
  }
}

function store(orders: SavedOrder[]) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(orders.slice(0, MAX_ORDERS)))
  } catch {
    // Không lưu được: đơn vẫn mở được bằng link quản lý vé.
  }
}

/** Ghi nhớ một đơn trên thiết bị. Đơn đã có thì giữ nguyên vị trí, chỉ cập nhật token. */
export function saveOrder(orderId: string, accessToken: string) {
  const orders = loadSavedOrders()
  const known = orders.find((x) => x.orderId === orderId)
  if (known?.accessToken === accessToken) return
  store(
    known
      ? orders.map((x) => (x.orderId === orderId ? { ...x, accessToken } : x))
      : [{ orderId, accessToken, savedAt: Date.now() }, ...orders],
  )
}

/** Xoá một đơn khỏi thiết bị này; đơn vẫn mở lại được bằng link quản lý vé. */
export function forgetOrder(orderId: string) {
  store(loadSavedOrders().filter((x) => x.orderId !== orderId))
}
