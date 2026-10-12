import { useMemo } from 'react'
import { t } from '../../i18n'

interface QrCodeProps {
  /** Text the pattern is derived from. Same value always yields the same pattern. */
  value: string
  size?: number
  className?: string
}

const GRID = 25
const FINDER = 7

// Small seeded PRNG so a given booking reference always renders the same code.
function seeded(text: string) {
  let h = 1779033703 ^ text.length
  for (let i = 0; i < text.length; i++) {
    h = Math.imul(h ^ text.charCodeAt(i), 3432918353)
    h = (h << 13) | (h >>> 19)
  }
  return () => {
    h = Math.imul(h ^ (h >>> 16), 2246822507)
    h = Math.imul(h ^ (h >>> 13), 3266489909)
    return ((h ^= h >>> 16) >>> 0) / 4294967296
  }
}

function inFinder(x: number, y: number) {
  const corner = (cx: number, cy: number) =>
    x >= cx && x < cx + FINDER + 1 && y >= cy && y < cy + FINDER + 1
  return corner(0, 0) || corner(GRID - FINDER - 1, 0) || corner(0, GRID - FINDER - 1)
}

/**
 * Decorative stand-in for a boarding QR. It is NOT scannable: real ticket codes come from the
 * backend later, at which point this component can be swapped for a real QR renderer.
 */
export default function QrCode({ value, size = 160, className = '' }: QrCodeProps) {
  const modules = useMemo(() => {
    const rand = seeded(value)
    const cells: { x: number; y: number }[] = []
    for (let y = 0; y < GRID; y++) {
      for (let x = 0; x < GRID; x++) {
        const center = x >= 9 && x <= 15 && y >= 9 && y <= 15
        if (inFinder(x, y) || center) continue
        if (rand() > 0.52) cells.push({ x, y })
      }
    }
    return cells
  }, [value])

  const finder = (x: number, y: number) => (
    <g transform={`translate(${x}, ${y})`}>
      <rect width="7" height="7" rx="1.2" fill="#0D2538" />
      <rect x="1" y="1" width="5" height="5" rx="0.8" fill="#fff" />
      <rect x="2" y="2" width="3" height="3" rx="0.6" fill="#0D2538" />
    </g>
  )

  return (
    <svg
      viewBox={`0 0 ${GRID} ${GRID}`}
      width={size}
      height={size}
      role="img"
      aria-label={t('Boarding QR for {code}', { code: value })}
      className={className}
      shapeRendering="crispEdges"
    >
      <rect width={GRID} height={GRID} fill="#fff" />
      {modules.map(({ x, y }) => (
        <rect key={`${x}-${y}`} x={x} y={y} width="1" height="1" fill="#0D2538" />
      ))}
      {finder(0, 0)}
      {finder(GRID - FINDER, 0)}
      {finder(0, GRID - FINDER)}
      <rect x="9.5" y="9.5" width="6" height="6" rx="1.4" fill="#147A7E" />
      <path
        d="M11 13.8c.7-.6 1.2-.6 1.5 0s.9.6 1.5 0"
        stroke="#fff"
        strokeWidth=".5"
        fill="none"
        strokeLinecap="round"
      />
      <path
        d="M11 12.6c.7-.6 1.2-.6 1.5 0s.9.6 1.5 0"
        stroke="#fff"
        strokeWidth=".5"
        fill="none"
        strokeLinecap="round"
      />
    </svg>
  )
}
