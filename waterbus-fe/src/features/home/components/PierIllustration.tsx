import { t } from '../../../i18n'

/** Hình nền bờ sông theo từng bến: cây xanh (Thanh Đa), nhà hàng ven sông (Hiệp Bình Chánh), cửa hàng và nhà kho (Linh Đông). */
function Bank({ code }: { code: string }) {
  if (code === 'ST03') {
    return (
      <g className="fill-teal-flow/60">
        {[40, 95, 150, 215, 270, 330].map((x, i) => (
          <g key={x}>
            <circle cx={x} cy={118 - (i % 2) * 8} r={26 - (i % 3) * 3} />
            <rect x={x - 2} y={130} width="4" height="14" className="fill-deep-river/50" />
          </g>
        ))}
      </g>
    )
  }
  if (code === 'ST04') {
    return (
      <g className="fill-deep-river/45">
        {[30, 130, 240].map((x) => (
          <g key={x}>
            <polygon points={`${x},128 ${x + 38},104 ${x + 76},128`} className="fill-coral-glow/70" />
            <rect x={x + 6} y={128} width="64" height="22" />
          </g>
        ))}
      </g>
    )
  }
  return (
    <g className="fill-deep-river/45">
      <rect x="28" y="108" width="92" height="42" />
      <polygon points="24,108 74,88 124,108" className="fill-signal-amber/70" />
      <rect x="150" y="118" width="70" height="32" />
      <rect x="236" y="100" width="58" height="50" />
      <rect x="306" y="122" width="56" height="28" />
    </g>
  )
}

/**
 * Hình minh hoạ thay ảnh thật khi bến chưa có ảnh (xem README thư mục ảnh bến). Đây là tranh vẽ phong cách, không
 * phải ảnh chụp của bến, nên có chú thích nhỏ để khách không nhầm.
 */
export default function PierIllustration({ code, name }: { code: string; name: string }) {
  return (
    <figure className="relative h-44 w-full overflow-hidden rounded-2xl bg-gradient-to-b from-sand-light to-mist">
      <svg viewBox="0 0 400 176" preserveAspectRatio="xMidYMid slice" className="h-full w-full" role="img" aria-label={name}>
        <circle cx="330" cy="38" r="20" className="fill-signal-amber/70" />
        <Bank code={code} />
        <rect x="0" y="150" width="400" height="26" className="fill-teal-flow/80" />
        <path
          d="M0 160 q20 -6 40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 M0 170 q20 -6 40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0 t40 0"
          className="fill-none stroke-white/40"
          strokeWidth="2"
        />
        <g className="fill-deep-river">
          <rect x="150" y="144" width="100" height="8" />
          <rect x="158" y="152" width="4" height="14" />
          <rect x="238" y="152" width="4" height="14" />
          <rect x="170" y="126" width="60" height="18" className="fill-white" />
          <rect x="176" y="132" width="10" height="6" />
          <rect x="195" y="132" width="10" height="6" />
          <rect x="214" y="132" width="10" height="6" />
        </g>
      </svg>
      <figcaption className="absolute inset-x-0 bottom-0 flex items-center justify-between bg-gradient-to-t from-deep-river/70 to-transparent px-3 pb-2 pt-6 text-xs text-white">
        <span className="font-semibold">{name}</span>
        <span className="opacity-80">{t('Illustration')}</span>
      </figcaption>
    </figure>
  )
}
