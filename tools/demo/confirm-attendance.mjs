// Demo: Admin xác nhận Captain demo có mặt cho các chuyến sắp chạy (bước bắt buộc trước khi Captain bấm Khởi hành).
// Chạy: node tools/demo/confirm-attendance.mjs [apiBase]   (mặc định http://localhost:5000, cần Node 18+)
// Chỉ dùng tài khoản seed của môi trường Development.
const base = process.argv[2] ?? 'http://localhost:5000'

async function call(method, path, token, body) {
  const response = await fetch(base + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  const text = await response.text()
  return { status: response.status, json: text ? JSON.parse(text) : null }
}

const login = async (email, password) => (await call('POST', '/api/v1/auth/login', null, { email, password })).json.token
const admin = await login('admin@waterbus.vn', 'Admin@123456!')
const captain = await login('captain@waterbus.vn', 'Captain@123456!')

const trips = (await call('GET', '/api/v1/trips/operations', captain)).json
const upcoming = trips.filter((t) => t.status === 'Scheduled' || t.status === 'Boarding')
let confirmed = 0
for (const trip of upcoming) {
  const attendance = (await call('GET', `/api/v1/trips/${trip.id}/captain-attendance`, admin)).json
  if (attendance?.state === 'Present' || attendance?.state === 'Late') continue
  const result = await call('POST', `/api/v1/trips/${trip.id}/captain-attendance`, admin, {
    captainId: attendance.captainId,
    assignmentVersion: attendance.assignmentVersion,
    reason: 'Demo: xác nhận có mặt',
  })
  if (result.status < 300) confirmed++
  else console.log(`Bỏ qua ${trip.routeName} ${trip.departureTime}: ${result.status}`)
}
console.log(`Đã xác nhận có mặt cho ${confirmed}/${upcoming.length} chuyến sắp chạy.`)
