import { t } from '../../i18n'

/** Fixed background of the live map: river, banks, bridge and district labels (920x660 canvas). */
export default function CorridorBackdrop() {
  return (
    <>
        <defs>
          <linearGradient id="riverDepth" x1="0%" y1="30%" x2="100%" y2="70%">
            <stop offset="0%" stopColor="#D7F3F6" />
            <stop offset="35%" stopColor="#C0ECF3" />
            <stop offset="65%" stopColor="#A2E2ED" />
            <stop offset="100%" stopColor="#8CD7E4" />
          </linearGradient>
          <radialGradient id="poiPulseWarm">
            <stop offset="0%" stopColor="#F08A6B" stopOpacity="0.7" />
            <stop offset="50%" stopColor="#E8B63E" stopOpacity="0.3" />
            <stop offset="100%" stopColor="#F08A6B" stopOpacity="0" />
          </radialGradient>
        </defs>

        <path
          d="M0 0 H335 C308 125 268 208 246 308 C220 422 284 532 296 660 H0 V0 Z"
          fill="#F4F7F8"
        />
        <path
          d="M920 0 H588 C610 112 662 222 682 332 C702 442 642 552 622 660 H920 V0 Z"
          fill="#EEF5F2"
        />
        <path
          d="M335 0 C308 125 268 208 246 308 C220 422 284 532 296 660 H622 C642 552 702 442 682 332 C662 222 610 112 588 0 Z"
          fill="url(#riverDepth)"
        />
        <g stroke="#fff" opacity="0.55" strokeLinecap="round" fill="none">
          <path
            d="M385 60 C365 140 338 220 328 300 C316 400 366 500 380 600"
            strokeWidth="2"
            strokeDasharray="24 20"
          />
          <path
            d="M445 40 C420 130 405 230 415 320 C425 410 470 510 488 610"
            strokeWidth="2.5"
            strokeDasharray="36 24"
          />
          <path
            d="M510 50 C490 145 482 235 496 325 C512 415 548 515 554 620"
            strokeWidth="2"
            strokeDasharray="28 22"
          />
        </g>
        <g opacity="0.28" fill="#0D2538">
          <rect x="40" y="90" width="48" height="38" rx="3" />
          <rect x="96" y="82" width="36" height="46" rx="3" />
          <rect x="30" y="180" width="62" height="42" rx="3" />
          <rect x="100" y="172" width="50" height="50" rx="3" />
          <rect x="35" y="290" width="52" height="45" rx="3" />
          <rect x="45" y="390" width="65" height="38" rx="3" />
          <rect x="30" y="490" width="75" height="44" rx="3" />
        </g>
        <g opacity="0.35" fill="#D2E7DC">
          <circle cx="780" cy="220" r="90" />
          <circle cx="750" cy="520" r="70" />
        </g>
        <path
          d="M335 0 C308 125 268 208 246 308 C220 422 284 532 296 660"
          stroke="#CBD5E1"
          strokeWidth="6"
          opacity="0.7"
        />
        <path
          d="M588 0 C610 112 662 222 682 332 C702 442 642 552 622 660"
          stroke="#CBD5E1"
          strokeWidth="6"
          opacity="0.7"
        />

        <g opacity="0.6">
          <path
            d="M 292 90 C 430 110, 500 120, 608 145"
            stroke="#fff"
            strokeWidth="7"
            strokeLinecap="round"
          />
          <path
            d="M 292 90 C 430 110, 500 120, 608 145"
            stroke="#475569"
            strokeWidth="3.5"
            strokeLinecap="round"
          />
          <rect x="395" y="78" width="86" height="18" rx="4" fill="#fff" opacity="0.9" />
          <text
            x="438"
            y="90"
            textAnchor="middle"
            fill="#43474C"
            fontFamily="Plus Jakarta Sans"
            fontSize="8.5"
            fontWeight="700"
            letterSpacing="0.3"
          >
            {t('BA SON BRIDGE')}
          </text>
        </g>

        <g transform="translate(36, 42)">
          <rect fill="#fff" height="36" rx="8" width="215" opacity="0.94" stroke="#CBD5E1" />
          <circle cx="16" cy="18" r="4" fill="#147A7E" />
          <text
            fill="#0D2538"
            fontFamily="Plus Jakarta Sans"
            fontSize="10.5"
            fontWeight="800"
            letterSpacing="0.5"
            x="28"
            y="16"
          >
            {t('DISTRICT 1')}
          </text>
          <text fill="#43474C" fontFamily="Inter" fontSize="9" fontWeight="500" x="28" y="28">
            {t('HISTORIC RIVERFRONT')}
          </text>
        </g>
        <g transform="translate(674, 42)">
          <rect fill="#fff" height="36" rx="8" width="220" opacity="0.94" stroke="#CBD5E1" />
          <circle cx="16" cy="18" r="4" fill="#4FC3D8" />
          <text
            fill="#0D2538"
            fontFamily="Plus Jakarta Sans"
            fontSize="10.5"
            fontWeight="800"
            letterSpacing="0.5"
            x="28"
            y="16"
          >
            {t('THU THIEM (D2)')}
          </text>
          <text fill="#43474C" fontFamily="Inter" fontSize="9" fontWeight="500" x="28" y="28">
            {t('PROMENADE & FINANCIAL CORRIDOR')}
          </text>
        </g>
    </>
  )
}
