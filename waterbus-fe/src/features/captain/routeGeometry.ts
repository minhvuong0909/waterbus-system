import type { LatLng } from '../../constant/riverPaths'

/** Chiếu tọa độ lên đoạn đường sông gần nhất, trả về tiến độ theo chiều tuyến (0..1). */
export function routeProgress(point: LatLng, path: LatLng[]): number {
  if (path.length < 2) return 0
  const scale = Math.cos((point[0] * Math.PI) / 180)
  const lengths = path
    .slice(1)
    .map((p, i) => Math.hypot(p[0] - path[i][0], (p[1] - path[i][1]) * scale))
  const total = lengths.reduce((a, b) => a + b, 0)
  let nearest = Infinity
  let progress = 0
  let travelled = 0
  lengths.forEach((length, i) => {
    const a = path[i]
    const dx = (path[i + 1][1] - a[1]) * scale
    const dy = path[i + 1][0] - a[0]
    const px = (point[1] - a[1]) * scale
    const py = point[0] - a[0]
    const t = length ? Math.max(0, Math.min(1, (px * dx + py * dy) / (length * length))) : 0
    const distance = Math.hypot(px - t * dx, py - t * dy)
    if (distance < nearest) {
      nearest = distance
      progress = travelled + t * length
    }
    travelled += length
  })
  return total ? progress / total : 0
}

/** Đường minh họa được chuẩn hóa; vị trí được ánh xạ theo chiều dài đường sông thực. */
export function illustrationPoint(progress: number): [number, number] {
  const segments = [
    [
      [100, 292],
      [290, 295],
      [300, 183],
      [485, 175],
    ],
    [
      [485, 175],
      [590, 168],
      [640, 250],
      [730, 155],
    ],
    [
      [730, 155],
      [790, 90],
      [755, 48],
      [880, 48],
    ],
  ]
  const u = Math.max(0, Math.min(1, progress)) * 3
  const segment = Math.min(2, Math.floor(u))
  const t = u - segment
  const p = segments[segment]
  const axis = (i: number) =>
    (1 - t) ** 3 * p[0][i] +
    3 * (1 - t) ** 2 * t * p[1][i] +
    3 * (1 - t) * t * t * p[2][i] +
    t ** 3 * p[3][i]
  return [axis(0), axis(1)]
}
