import { useEffect, useState } from 'react'
import { Icon } from '../ui'
import { t } from '../../i18n'

interface AudioPlayerProps {
  title: string
  durationSec: number
  startAtSec?: number
  languages: string[]
  /** Small label above the title. Defaults to "Audio Guide". */
  eyebrow?: string
  subtitle?: string
  thumbnail?: string
  /** `dark` is for use on Deep River backgrounds. */
  tone?: 'light' | 'dark'
  className?: string
}

const fmt = (sec: number) =>
  `${Math.floor(sec / 60)}:${String(Math.floor(sec % 60)).padStart(2, '0')}`

/**
 * Simulated audio player: no real audio, a timer advances the progress while "playing".
 * Shared by the live tracking and explore screens.
 * Pass a `key` (the episode id) so switching episodes remounts it and restarts playback.
 */
export default function AudioPlayer({
  title,
  durationSec,
  startAtSec = 0,
  languages,
  eyebrow = t('Audio Guide'),
  subtitle,
  thumbnail,
  tone = 'light',
  className = '',
}: AudioPlayerProps) {
  const [position, setPosition] = useState(startAtSec)
  const [playing, setPlaying] = useState(startAtSec < durationSec)
  const [language, setLanguage] = useState(languages[0])
  const dark = tone === 'dark'

  // Playback stops by itself at the end, without touching state from inside an effect.
  const isPlaying = playing && position < durationSec

  useEffect(() => {
    if (!isPlaying) return
    const timer = setInterval(() => setPosition((p) => Math.min(p + 1, durationSec)), 1000)
    return () => clearInterval(timer)
  }, [isPlaying, durationSec])

  const seek = (delta: number) => setPosition((p) => Math.min(Math.max(p + delta, 0), durationSec))
  const toggle = () => {
    if (position >= durationSec) {
      setPosition(0)
      setPlaying(true)
    } else {
      setPlaying((v) => !v)
    }
  }
  const percent = durationSec ? (position / durationSec) * 100 : 0

  const shell = dark
    ? 'border-white/10 bg-white/5 text-on-primary backdrop-blur-sm'
    : 'border-outline-variant/40 bg-mist/60'
  const trackBg = dark ? 'rgba(255,255,255,0.18)' : '#e3e2e4'
  const ctrl = dark
    ? 'bg-white/10 text-white hover:bg-white/20'
    : 'bg-white text-deep-river shadow-sm hover:bg-surface-container-low'

  return (
    <div className={`rounded-2xl border p-space-md ${shell} ${className}`.trim()}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-start gap-3">
          {thumbnail && (
            <img
              alt=""
              aria-hidden="true"
              src={thumbnail}
              className="h-14 w-14 shrink-0 rounded-lg object-cover"
            />
          )}
          <div className="min-w-0">
            <div
              className={`text-[10px] font-bold uppercase tracking-wider ${
                dark ? 'text-sky-aqua' : 'text-teal-flow'
              }`}
            >
              {eyebrow}
            </div>
            <div
              className={`font-headline-sm text-base font-bold leading-snug ${
                dark ? 'text-white' : 'text-deep-river'
              }`}
            >
              {title}
            </div>
            {subtitle && (
              <div className={`text-xs ${dark ? 'text-sand-light/70' : 'text-on-surface-variant'}`}>
                {subtitle}
              </div>
            )}
          </div>
        </div>
        <div
          className={`flex shrink-0 gap-1 rounded-lg p-1 text-[11px] font-semibold ${
            dark ? 'bg-white/10' : 'bg-white'
          }`}
          role="group"
          aria-label={t('Audio language')}
        >
          {languages.map((l) => (
            <button
              key={l}
              type="button"
              aria-pressed={language === l}
              onClick={() => setLanguage(l)}
              className={`rounded px-2 py-0.5 ${
                language === l
                  ? dark
                    ? 'bg-teal-flow text-white'
                    : 'bg-deep-river text-white'
                  : dark
                  ? 'text-sand-light/70 hover:text-white'
                  : 'text-on-surface-variant hover:text-deep-river'
              }`}
            >
              {l}
            </button>
          ))}
        </div>
      </div>

      <input
        type="range"
        min={0}
        max={durationSec}
        value={position}
        onChange={(e) => setPosition(Number(e.target.value))}
        aria-label={t('Audio position')}
        className="mt-space-md h-1.5 w-full cursor-pointer appearance-none rounded-full accent-teal-flow"
        style={{
          background: `linear-gradient(to right, #4FC3D8 ${percent}%, ${trackBg} ${percent}%)`,
        }}
      />
      <div className="mt-1 flex justify-between text-xs font-semibold">
        <span className={dark ? 'text-sky-aqua' : 'text-teal-flow'}>{fmt(position)}</span>
        <span className={dark ? 'text-sand-light/70' : 'text-on-surface-variant'}>
          {fmt(durationSec)}
        </span>
      </div>

      <div className="mt-space-sm flex items-center justify-center gap-space-md">
        <button
          type="button"
          aria-label={t('Back 10 seconds')}
          onClick={() => seek(-10)}
          className={`flex h-9 w-9 items-center justify-center rounded-full ${ctrl}`}
        >
          <Icon name="replay_10" className="text-[20px]" />
        </button>
        <button
          type="button"
          aria-label={isPlaying ? t('Pause') : t('Play')}
          onClick={toggle}
          className="flex h-12 w-12 items-center justify-center rounded-full bg-teal-flow text-on-primary shadow-md hover:bg-secondary"
        >
          <Icon name={isPlaying ? 'pause' : 'play_arrow'} className="text-[26px]" filled />
        </button>
        <button
          type="button"
          aria-label={t('Forward 10 seconds')}
          onClick={() => seek(10)}
          className={`flex h-9 w-9 items-center justify-center rounded-full ${ctrl}`}
        >
          <Icon name="forward_10" className="text-[20px]" />
        </button>
      </div>
    </div>
  )
}
