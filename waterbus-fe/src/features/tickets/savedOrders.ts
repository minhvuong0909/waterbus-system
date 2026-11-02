import { ROUTES } from '../../routes/routes'

/** Một đơn đã mua trên thiết bị này, kèm token Guest để mở lại (BR-ID-02). */
export interface SavedOrder {
  orderId: string
  accessToken: string
  /** ms since epoch, đơn mới nhất đứng đầu. */
  savedAt: number
}

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
            typeof x?.orderId === 'string' && typeof x?.accessToken === 'string' && typeof x?.savedAt === 'number',
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

/** Đường dẫn Manage Order secure link của một đơn: token trong path, mã đơn trong query. */
export function manageLinkPath(orderId: string, accessToken: string): string {
  return `${ROUTES.manageByLink.replace(':token', encodeURIComponent(accessToken))}?orderId=${encodeURIComponent(orderId)}`
}

/** Link đầy đủ để khách lưu lại hoặc gửi cho chính mình. */
export function manageLinkUrl(orderId: string, accessToken: string): string {
  return `${window.location.origin}${manageLinkPath(orderId, accessToken)}`
}

/**
 * Đọc mã đơn và token từ một link quản lý vé khách dán vào (đầy đủ hoặc chỉ phần đường dẫn).
 * Trả về null khi không phải link quản lý vé.
 */
export function parseManageLink(input: string): { orderId: string; accessToken: string } | null {
  const text = input.trim()
  if (!text) return null
  let url: URL
  try {
    url = new URL(text, window.location.origin)
  } catch {
    return null
  }
  const prefix = ROUTES.manageByLink.replace(':token', '')
  if (!url.pathname.startsWith(prefix)) return null
  const segment = url.pathname.slice(prefix.length).replace(/\/+$/, '')
  const orderId = url.searchParams.get('orderId')
  if (!segment || segment.includes('/') || !orderId) return null
  try {
    return { orderId, accessToken: decodeURIComponent(segment) }
  } catch {
    return null
  }
}
