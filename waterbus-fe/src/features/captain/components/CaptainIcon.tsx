import type { ReactNode } from 'react'

const paths: Record<string, ReactNode> = {
  speed: (
    <>
      <path d="M4 17a9 9 0 1 1 16 0M12 13l4-5M6 10l1 1m10 0 1-1M12 4v2" />
      <circle cx="12" cy="14" r="2" />
    </>
  ),
  location_on: (
    <>
      <path d="M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 1 1 14 0Z" />
      <circle cx="12" cy="10" r="2" />
    </>
  ),
  groups: (
    <>
      <circle cx="9" cy="7" r="3" />
      <path d="M3 21v-4a6 6 0 0 1 12 0v4ZM16 4a3 3 0 0 1 0 6m2 4a5 5 0 0 1 3 5v2" />
    </>
  ),
  satellite_alt: (
    <>
      <path d="m7 4 3 3-3 3-3-3Zm10 10 3 3-3 3-3-3ZM9 9l6 6m-6 2-3 3m3-1-4-4M14 3a7 7 0 0 1 7 7M14 7a3 3 0 0 1 3 3" />
    </>
  ),
  location_off: (
    <>
      <path d="m3 3 18 18M5 9c-.5 5 7 12 7 12s2-2 4-5M8 4a7 7 0 0 1 11 6c0 1-.4 2-1 3" />
    </>
  ),
  map: (
    <>
      <path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2Zm6-2v16m6-14v16" />
    </>
  ),
  my_location: (
    <>
      <circle cx="12" cy="12" r="7" />
      <circle cx="12" cy="12" r="2" />
      <path d="M12 2v3m0 14v3M2 12h3m14 0h3" />
    </>
  ),
  location_searching: (
    <>
      <circle cx="12" cy="12" r="7" />
      <path d="M12 2v3m0 14v3M2 12h3m14 0h3" />
    </>
  ),
  directions_boat: (
    <>
      <path d="m3 13 9-3 9 3-3 6H6Zm4-1V7h10v5M9 7V4h6v3M2 21q3-2 5 0 3-2 5 0 3-2 5 0 3-2 5 0" />
    </>
  ),
  sailing: (
    <>
      <path d="M12 3v14H4Zm2 2 6 12h-6M3 20h18l-3 2H6Z" />
    </>
  ),
  schedule: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 6v6l4 2" />
    </>
  ),
  check: <path d="m5 12 4 4L19 6" />,
  check_circle: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="m7 12 3 3 7-7" />
    </>
  ),
  expand_more: <path d="m6 9 6 6 6-6" />,
  logout: (
    <>
      <path d="M10 4H4v16h6m-1-8h12m-4-4 4 4-4 4" />
    </>
  ),
  sync: (
    <>
      <path d="M20 8a8 8 0 0 0-14-3L3 8m0-5v5h5m-4 8a8 8 0 0 0 14 3l3-3m0 5v-5h-5" />
    </>
  ),
  pause_circle: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M9 8v8m6-8v8" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v6m0-10v.5" />
    </>
  ),
  settings: (
    <>
      <path d="m9 3 1-1h4l1 3 3 1 3 1-1 4 2 2-2 4-3 1-1 3h-5l-1-3-3-1-3-1 1-4-2-2 2-4 3-1Z" />
      <circle cx="12" cy="12" r="4" />
    </>
  ),
  play_arrow: <path d="m7 3 14 9L7 21Z" />,
  sports_score: (
    <>
      <path d="M5 22V3m0 1c5-4 9 4 15 0v11c-6 4-10-4-15 0" />
      <path d="m10 3 0 11m5-10v12M5 9c5-4 9 4 15 0" />
    </>
  ),
  e911_emergency: (
    <>
      <path d="M7 18v-6a5 5 0 0 1 10 0v6M4 18h16v4H4ZM12 1v3M3 5l2 2m14 0 2-2M1 12h3m16 0h3" />
    </>
  ),
  route: (
    <>
      <circle cx="6" cy="5" r="3" />
      <circle cx="18" cy="19" r="3" />
      <path d="M6 8v5a4 4 0 0 0 4 4h4M18 3v9" />
    </>
  ),
  verified_user: (
    <>
      <path d="m12 2 9 4v6c0 6-9 10-9 10S3 18 3 12V6Z" />
      <path d="m8 12 3 3 5-6" />
    </>
  ),
  notifications: (
    <>
      <path d="M5 17h14l-2-3V9a5 5 0 0 0-10 0v5Zm5 3a2 2 0 0 0 4 0M12 2v2" />
    </>
  ),
  wifi_off: (
    <>
      <path d="m3 3 18 18M2 8l3-2m3-2a18 18 0 0 1 14 4M6 12l3-2m4-1a12 12 0 0 1 5 3m-8 4a4 4 0 0 1 4 0" />
      <circle cx="12" cy="20" r=".5" />
    </>
  ),
  sync_problem: (
    <>
      <path d="M20 8a8 8 0 0 0-14-3L3 8m0-5v5h5m-4 8a8 8 0 0 0 14 3l3-3m0 5v-5h-5M12 7v5m0 3v1" />
    </>
  ),
}

/** SVG cục bộ để không phụ thuộc bộ icon font đã được cắt gọn của ứng dụng. */
export default function CaptainIcon({ name }: { name: string }) {
  return (
    <svg
      className="captain-icon material-symbols-outlined"
      width="1em"
      height="1em"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name] ?? paths.info}
    </svg>
  )
}
