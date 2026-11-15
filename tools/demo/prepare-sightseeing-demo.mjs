// Demo: chuẩn bị để trang Captain hiện đúng một chuyến THAM QUAN sắp chạy.
// Thuyền trưởng gắn cố định với tàu SWB-01 và chỉ thấy chuyến hiện tại của tàu (chuyến sắp khởi hành sớm nhất), còn Admin phân tàu cho
// từng chuyến. Script đóng vai Admin:
//   1. chọn chuyến tham quan (SS01/SS02) sắp khởi hành sớm nhất của SWB-01;
//   2. chuyển các chuyến Scheduled khác của SWB-01 khởi hành trước nó sang tàu khác (PUT /api/v1/admin/trips/{id}/boat);
//   3. xác nhận Captain có mặt cho chuyến đó (POST /api/v1/trips/{id}/captain-attendance).
// Chạy: node tools/demo/prepare-sightseeing-demo.mjs [apiBase]   (Node 18+, chỉ dùng tài khoản seed của Development)
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

const admin = (await call('POST', '/api/v1/auth/login', null, { email: 'admin@waterbus.vn', password: 'Admin@123456!' })).json.token
const boats = (await call('GET', '/api/v1/tracking/boats', admin)).json
const captainBoat = boats.find((b) => b.boatCode === 'SWB-01')
const spare = boats.filter((b) => b.boatCode !== 'SWB-01')
if (!captainBoat) throw new Error('Không thấy tàu SWB-01 (API đã seed xong chưa?)')

const now = Date.now()
const trips = (await call('GET', '/api/v1/trips/operations?from=' + new Date(now - 3600_000).toISOString() + '&to=' + new Date(now + 3 * 86400_000).toISOString(), admin)).json
const mine = trips
  .filter((t) => t.boatCode === 'SWB-01' && t.status === 'Scheduled' && new Date(t.departureTime).getTime() > now - 1800_000)
  .sort((a, b) => a.departureTime.localeCompare(b.departureTime))
const target = mine.find((t) => t.tripType === 'Sightseeing')
if (!target) throw new Error('SWB-01 không còn chuyến tham quan sắp chạy')

let moved = 0
for (const trip of mine.filter((t) => t.departureTime < target.departureTime)) {
  let done = false
  for (const boat of spare) {
    const result = await call('PUT', `/api/v1/admin/trips/${trip.id}/boat`, admin, { boatId: boat.boatId })
    if (result.status < 300) {
      done = true
      moved++
      break
    }
  }
  if (!done) console.log(`Không chuyển được ${trip.routeName} ${trip.departureTime} sang tàu khác (trùng giờ hoặc thiếu ghế).`)
}

const attendance = (await call('GET', `/api/v1/trips/${target.id}/captain-attendance`, admin)).json
if (attendance.state !== 'Present' && attendance.state !== 'Late') {
  await call('POST', `/api/v1/trips/${target.id}/captain-attendance`, admin, {
    captainId: attendance.captainId,
    assignmentVersion: attendance.assignmentVersion,
    reason: 'Demo: xác nhận có mặt',
  })
}
console.log(`Đã chuẩn bị chuyến ${target.routeName} (${target.routeCode}) lúc ${target.departureTime}; chuyển ${moved} chuyến khác sang tàu khác.`)
console.log('Đăng nhập Captain, tải lại /captain: trang sẽ hiện đúng chuyến này.')
