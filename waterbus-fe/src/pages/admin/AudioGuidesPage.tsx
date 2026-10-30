import { useEffect, useId, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { ActionButton, Notice, PageHeader, Panel, SelectField } from '../../features/admin/components/AdminUi'
import { MAX_NARRATION_LENGTH, NARRATION_VOICES } from '../../features/admin/audioGuide'
import type { NarrationPoi, NarrationVoice } from '../../features/admin/audioGuide'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  fetchNarrationAudio,
  generateNarration,
  getNarrationPois,
  publishNarration,
} from '../../features/admin/services/audioGuideService'
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
  onPublished,
}: {
  poi: NarrationPoi
  draft: Draft
  preview?: Preview
  onDraft: (draft: Draft) => void
  onPreview: (preview: Preview) => void
  onPublished: () => void
}) {
  const textId = useId()
  const { run, busy, error } = useSubmit()
  const publishing = useSubmit()
  const [published, setPublished] = useState(false)
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
    if (result) {
      setPublished(false)
      onPreview(result)
    }
  }

  const publish = async () => {
    if (!preview) return
    const result = await publishing.run(() => publishNarration(poi.poiId, preview.narration))
    if (result) {
      setPublished(true)
      onPublished()
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      <div>
        <h2 className="text-lg font-bold text-deep-river">{poi.name}</h2>
        <p className="mt-1 text-sm text-on-surface-variant">
          {poi.isPublished ? t('This landmark has a published audio guide.') : t('This landmark has no published audio guide yet.')}
        </p>
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
          {publishing.error && <Notice tone="error">{publishing.error}</Notice>}
          {published && <Notice tone="success">{t('Audio guide published.')}</Notice>}
          <ActionButton
            icon="publish"
            disabled={publishing.busy || published || preview.text !== text}
            onClick={publish}
            className="w-full"
          >
            {publishing.busy ? t('Publishing…') : t('Publish this audio')}
          </ActionButton>
        </div>
      )}
    </form>
  )
}

export default function AudioGuidesPage() {
  useDocumentTitle('Audio guides')
  const [version, reload] = useReload()
  const pois = useFetch(getNarrationPois, [version])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [previews, setPreviews] = useState<Record<string, Preview>>({})

  // Object URLs keep the MP3 in memory: release them when the page closes.
  const urls = useRef<string[]>([])
  useEffect(() => () => urls.current.forEach((url) => URL.revokeObjectURL(url)), [])

  if (pois.error) return <ErrorState onRetry={pois.retry} />
  if (!pois.data) return <PageLoader />

  const selected = pois.data.find((p) => p.poiId === selectedId)

  const savePreview = (poiId: string, preview: Preview) => {
    setPreviews((current) => {
      const old = current[poiId]
      if (old) URL.revokeObjectURL(old.url)
      return { ...current, [poiId]: preview }
    })
    urls.current.push(preview.url)
  }

  return (
    <>
      <PageHeader
        title="Audio guides"
        description="Write what the boat says at each landmark, pick a voice, listen to it and publish it. Azure turns the script into speech."
      />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
        <div className="lg:col-span-7">
          {pois.data.length === 0 ? (
            <p className="text-sm text-on-surface-variant">{t('There are no landmarks yet.')}</p>
          ) : (
            <div className="flex flex-wrap gap-2" role="group" aria-label={t('Landmarks')}>
              {pois.data.map((poi) => (
                <button
                  key={poi.poiId}
                  type="button"
                  aria-pressed={poi.poiId === selectedId}
                  onClick={() => setSelectedId(poi.poiId)}
                  className={`inline-flex min-h-[40px] items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-semibold transition-colors ${
                    poi.poiId === selectedId
                      ? 'border-deep-river bg-deep-river text-white'
                      : 'border-outline-variant bg-white text-deep-river hover:bg-surface-container-low'
                  }`}
                >
                  <Icon name={poi.isPublished ? 'check_circle' : previews[poi.poiId] ? 'headphones' : 'photo_camera'} className="text-[18px]" />
                  {poi.name}
                </button>
              ))}
            </div>
          )}
        </div>

        <Panel className="lg:col-span-5">
          {selected ? (
            <NarrationEditor
              key={selected.poiId}
              poi={selected}
              draft={drafts[selected.poiId] ?? EMPTY_DRAFT}
              preview={previews[selected.poiId]}
              onDraft={(draft) => setDrafts((d) => ({ ...d, [selected.poiId]: draft }))}
              onPreview={(preview) => savePreview(selected.poiId, preview)}
              onPublished={reload}
            />
          ) : (
            <div className="flex min-h-[240px] flex-col items-center justify-center gap-2 text-center text-sm text-on-surface-variant">
              <Icon name="touch_app" className="text-[32px] text-teal-flow" />
              {t('Pick a landmark to write its narration.')}
            </div>
          )}
        </Panel>
      </div>
    </>
  )
}
