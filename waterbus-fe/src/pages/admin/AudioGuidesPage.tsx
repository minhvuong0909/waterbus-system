import { useEffect, useId, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '../../components/ui'
import { ActionButton, Notice, PageHeader, Panel, SelectField } from '../../features/admin/components/AdminUi'
import PoiMap from '../../features/admin/components/PoiMap'
import { MAX_NARRATION_LENGTH, NARRATION_POIS, NARRATION_VOICES } from '../../features/admin/audioGuide'
import type { NarrationPoi, NarrationVoice } from '../../features/admin/audioGuide'
import { useSubmit } from '../../features/admin/hooks/useAdmin'
import { getStations } from '../../features/admin/services/adminService'
import { fetchNarrationAudio, generateNarration } from '../../features/admin/services/audioGuideService'
import type { GeneratedNarration } from '../../features/admin/services/audioGuideService'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'

interface Draft {
  text: string
  voice: NarrationVoice
}

interface Preview {
  narration: GeneratedNarration
  /** Object URL of the MP3, revoked when replaced or when the page closes. */
  url: string
  /** The script it was generated from, to tell when the text was changed afterwards. */
  text: string
}

const EMPTY_DRAFT: Draft = { text: '', voice: NARRATION_VOICES[0].id }

const voiceLabel = (id: string) => NARRATION_VOICES.find((v) => v.id === id)?.label ?? id

function NarrationEditor({
  poi,
  draft,
  preview,
  onDraft,
  onPreview,
}: {
  poi: NarrationPoi
  draft: Draft
  preview?: Preview
  onDraft: (draft: Draft) => void
  onPreview: (preview: Preview) => void
}) {
  const textId = useId()
  const { run, busy, error } = useSubmit()
  const [showError, setShowError] = useState(false)
  const text = draft.text.trim()
  const textError = showError && !text ? t('Enter the narration to read at this landmark.') : undefined

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!text) {
      setShowError(true)
      return
    }
    const result = await run(async () => {
      const narration = await generateNarration(text, draft.voice)
      const audio = await fetchNarrationAudio(narration.assetKey)
      return { narration, url: URL.createObjectURL(audio), text }
    })
    if (result) onPreview(result)
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      <div>
        <h2 className="text-lg font-bold text-deep-river">{t(poi.title)}</h2>
        <p className="mt-1 text-sm text-on-surface-variant">{t(poi.description)}</p>
      </div>

      <div>
        <label htmlFor={textId} className="mb-1 block text-xs font-semibold text-on-surface-variant">
          {t('Narration script')}
        </label>
        <textarea
          id={textId}
          rows={8}
          maxLength={MAX_NARRATION_LENGTH}
          value={draft.text}
          onChange={(e) => onDraft({ ...draft, text: e.target.value })}
          aria-invalid={Boolean(textError)}
          placeholder={t('For example: On your right is the Ba Son waterfront, once the largest shipyard of Saigon…')}
          className="w-full rounded-xl border border-outline-variant/70 bg-white px-3 py-2 text-sm leading-relaxed text-deep-river transition-colors focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15"
        />
        <div className="mt-1 flex justify-between gap-3 text-[11px] text-on-surface-variant">
          {textError ? (
            <span role="alert" className="text-coral-glow">{textError}</span>
          ) : (
            <span>{t('Played on board when the boat comes near this landmark.')}</span>
          )}
          <span className="shrink-0 tabular-nums">
            {draft.text.length.toLocaleString('vi-VN')}/{MAX_NARRATION_LENGTH.toLocaleString('vi-VN')}
          </span>
        </div>
      </div>

      <SelectField
        label="Voice"
        value={draft.voice}
        onChange={(e) => onDraft({ ...draft, voice: e.target.value as NarrationVoice })}
      >
        {NARRATION_VOICES.map((v) => (
          <option key={v.id} value={v.id}>
            {t(v.label)}
          </option>
        ))}
      </SelectField>

      {error && <Notice tone="error">{error}</Notice>}

      <ActionButton type="submit" icon="volume_up" disabled={busy} className="w-full">
        {busy ? t('Generating audio…') : t('Generate and listen')}
      </ActionButton>

      {preview && (
        <div className="space-y-2 rounded-xl bg-surface-container-low p-3">
          <div className="flex items-center gap-2 text-sm font-semibold text-deep-river">
            <Icon name="headphones" className="text-[20px] text-teal-flow" />
            {t('Listen before publishing')}
          </div>
          {/* Autoplay right after generating: the admin pressed the button to hear it. */}
          <audio key={preview.url} src={preview.url} controls autoPlay className="w-full" />
          <p className="text-xs text-on-surface-variant">
            {t('{voice} • {seconds} s • {size} KB', {
              voice: t(voiceLabel(preview.narration.voiceName)),
              seconds: preview.narration.durationSeconds,
              size: Math.max(1, Math.round(preview.narration.byteLength / 1024)),
            })}
          </p>
          {preview.text !== text && (
            <p className="text-xs text-coral-glow">{t('The script changed after this audio. Generate again to hear it.')}</p>
          )}
        </div>
      )}
    </form>
  )
}

export default function AudioGuidesPage() {
  useDocumentTitle('Audio guides')
  const stations = useFetch(getStations)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [previews, setPreviews] = useState<Record<string, Preview>>({})
  const selected = NARRATION_POIS.find((p) => p.id === selectedId)

  // Object URLs keep the MP3 in memory: release them when the page closes.
  const urls = useRef<string[]>([])
  useEffect(() => () => urls.current.forEach((url) => URL.revokeObjectURL(url)), [])

  const savePreview = (poiId: string, preview: Preview) => {
    setPreviews((current) => {
      const old = current[poiId]
      if (old) URL.revokeObjectURL(old.url)
      return { ...current, [poiId]: preview }
    })
    urls.current.push(preview.url)
  }

  const corridor = (stations.data ?? [])
    .filter((s) => s.isActive !== false)
    .sort((a, b) => a.orderIndex - b.orderIndex)

  return (
    <>
      <PageHeader
        title="Audio guides"
        description="Write what the boat says at each landmark, pick a voice and listen to it. Azure turns the script into speech."
      />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <div className="space-y-3 lg:col-span-7">
          <PoiMap
            pois={NARRATION_POIS}
            stations={corridor}
            selectedId={selectedId}
            readyIds={new Set(Object.keys(previews))}
            onSelect={setSelectedId}
          />
          <div className="flex flex-wrap gap-2" role="group" aria-label={t('Landmarks')}>
            {NARRATION_POIS.map((poi) => (
              <button
                key={poi.id}
                type="button"
                aria-pressed={poi.id === selectedId}
                onClick={() => setSelectedId(poi.id)}
                className={`inline-flex min-h-[40px] items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-semibold transition-colors ${
                  poi.id === selectedId
                    ? 'border-deep-river bg-deep-river text-white'
                    : 'border-outline-variant bg-white text-deep-river hover:bg-surface-container-low'
                }`}
              >
                <Icon name={previews[poi.id] ? 'check_circle' : 'photo_camera'} className="text-[18px]" />
                {t(poi.title)}
              </button>
            ))}
          </div>
        </div>

        <Panel className="lg:col-span-5">
          {selected ? (
            <NarrationEditor
              key={selected.id}
              poi={selected}
              draft={drafts[selected.id] ?? EMPTY_DRAFT}
              preview={previews[selected.id]}
              onDraft={(draft) => setDrafts((d) => ({ ...d, [selected.id]: draft }))}
              onPreview={(preview) => savePreview(selected.id, preview)}
            />
          ) : (
            <div className="flex min-h-[240px] flex-col items-center justify-center gap-2 text-center text-sm text-on-surface-variant">
              <Icon name="touch_app" className="text-[32px] text-teal-flow" />
              {t('Pick a landmark on the map to write its narration.')}
            </div>
          )}
        </Panel>
      </div>
    </>
  )
}
