interface LogoProps {
  className?: string
}

// Source: docs/stitch/.../smart_waterbus_logo/code.html
export default function Logo({ className = 'h-9 w-auto' }: LogoProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 240 50"
      fill="none"
      role="img"
      aria-label="Smart Waterbus"
      className={className}
    >
      <g transform="translate(4, 5)">
        <path d="M4 22C12 10 24 6 38 7C28 12 24 16 18 22H4Z" fill="#147A7E" />
        <path d="M12 25C22 17 34 15 46 16C36 21 30 25 24 31H12Z" fill="#4FC3D8" />
        <path d="M22 34C28 28 36 26 44 26C38 30 35 33 30 37H22Z" fill="#0D2538" />
      </g>
      <text
        x="56"
        y="23"
        fontFamily="'Plus Jakarta Sans', sans-serif"
        fontSize="16"
        fontWeight="800"
        fill="#0D2538"
        letterSpacing="0.5"
      >
        SMART
      </text>
      <text
        x="56"
        y="38"
        fontFamily="'Plus Jakarta Sans', sans-serif"
        fontSize="15"
        fontWeight="700"
        fill="#147A7E"
        letterSpacing="1.5"
      >
        WATERBUS
      </text>
    </svg>
  )
}
