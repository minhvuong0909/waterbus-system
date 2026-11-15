import { useEffect, useId, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ErrorState, Icon, Notice, PageHeader, PageLoader, Panel, SelectField } from '../../components/ui'
import StationAnnouncementsTab from '../../features/admin/components/StationAnnouncementsTab'
import SimulatorConfigTab from '../../features/admin/components/SimulatorConfigTab'
import { getSimulatorConfig } from '../../features/admin/services/simulatorService'
import type { StopAnnouncementConfig } from '../../features/admin/services/simulatorService'
import { NARRATION_VOICES, getSuggestionForPoi } from '../../features/admin/utils/audioGuide'
import type { NarrationPoi, NarrationVoice } from '../../features/admin/utils/audioGuide'
import {
  fetchNarrationAudio,
  generateNarration,
  getNarrationPois,
  publishNarration,
} from '../../features/admin/services/audioGuideService'
import type { GeneratedNarration } from '../../features/admin/services/audioGuideService'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
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
  maxAudioSeconds,
  maxCharacters,
  segmentDuration,
  onDraft,
  onPreview,
  onPublished,
}: {
  poi: NarrationPoi
  draft: Draft
  preview?: Preview
  maxAudioSeconds: number
  maxCharacters: number
  segmentDuration: number
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
  const suggestedScript = getSuggestionForPoi(poi.name)

  const handleApplySuggestion = () => {
    onDraft({ ...draft, text: suggestedScript })
    setShowError(false)
  }

  const handleQuickGenerate = async () => {
    const textToUse = suggestedScript
    onDraft({ ...draft, text: textToUse })
    setShowError(false)
    const result = await run(async () => {
      const narration = await generateNarration(textToUse, draft.voice)
      const audio = await fetchNarrationAudio(narration.assetKey)
      return { narration, url: URL.createObjectURL(audio), text: textToUse }
    })
    if (result) {
      setPublished(false)
      onPreview(result)
    }
  }

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
          {poi.isPublished ? t('Địa điểm này đã có bản thuyết minh chính thức.') : t('Địa điểm này chưa có bản thuyết minh.')}
        </p>
      </div>

      {/* Khung Kịch bản Gợi ý Du lịch */}
      <div className="rounded-2xl border border-teal-200/90 bg-teal-50/50 p-4 space-y-3 shadow-2xs">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-1.5 font-bold text-xs text-teal-950">
            <span className="text-sm">✨</span>
            <span>Gợi ý kịch bản thuyết minh du lịch:</span>
          </div>
          <span className="text-3xs font-semibold uppercase tracking-wider text-teal-800 bg-teal-200/70 px-2 py-0.5 rounded">
            Đề xuất sẵn
          </span>
        </div>

        <p className="text-xs text-deep-river leading-relaxed bg-white/90 p-3 rounded-xl border border-teal-200/50 italic shadow-2xs">
          &ldquo;{suggestedScript}&rdquo;
        </p>

        <div className="flex flex-wrap items-center gap-2 pt-0.5">
          <button
            type="button"
            onClick={handleApplySuggestion}
            className="inline-flex items-center gap-1.5 rounded-xl bg-teal-700 hover:bg-teal-800 text-white font-semibold text-xs py-2 px-3.5 transition-colors shadow-2xs"
          >
            <span>✍️</span>
            <span>Dùng gợi ý này</span>
          </button>

          <button
            type="button"
            disabled={busy}
            onClick={handleQuickGenerate}
            className="inline-flex items-center gap-1.5 rounded-xl bg-white border border-teal-300 hover:bg-teal-50 text-teal-900 font-semibold text-xs py-2 px-3.5 transition-colors shadow-2xs"
          >
            <span>⚡</span>
            <span>{busy ? 'Đang tạo audio...' : 'Tạo & Nghe thử ngay'}</span>
          </button>
        </div>
      </div>

      <div>
        <div className="mb-1 flex items-center justify-between">
          <label htmlFor={textId} className="block text-xs font-semibold text-on-surface-variant">
            {t('Narration script')}
          </label>
          <span className="text-2xs font-semibold text-teal-800 bg-teal-50 border border-teal-200 px-2 py-0.5 rounded-md">
            Tối đa ~{maxAudioSeconds}s ({maxCharacters} ký tự)
          </span>
        </div>
        <textarea
          id={textId}
          rows={6}
          maxLength={maxCharacters}
          value={draft.text}
          onChange={(e) => onDraft({ ...draft, text: e.target.value })}
          aria-invalid={Boolean(textError)}
          placeholder={t('For example: On your right is the Ba Son waterfront, once the largest shipyard of Saigon…')}
          className="w-full rounded-xl border border-outline-variant/70 bg-white px-3 py-2 text-sm leading-relaxed text-deep-river transition-colors focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15"
        />
        <div className="mt-1 flex items-center justify-between gap-3 text-2xs text-on-surface-variant">
          {textError ? (
            <span role="alert" className="text-coral-glow font-semibold">{textError}</span>
          ) : (
            <span>
              Phát khi tàu đến gần địa điểm (chặng ~{segmentDuration}s phân bổ từ chuyến đi, trừ thời gian rời bến & cập bến).
            </span>
          )}
          <span className={`shrink-0 tabular-nums font-semibold ${
            draft.text.length >= maxCharacters ? 'text-coral-glow font-bold' : 'text-deep-river'
          }`}>
            {draft.text.length.toLocaleString('vi-VN')} / {maxCharacters.toLocaleString('vi-VN')} ký tự
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

      <Button size="sm" type="submit" icon="volume_up" disabled={busy} className="w-full">
        {busy ? t('Generating audio…') : t('Generate and listen')}
      </Button>

      {preview && (
        <div className="space-y-3 rounded-xl bg-surface-container-low p-3.5 border border-outline-variant/40">
          <div className="flex items-center gap-2 text-sm font-semibold text-deep-river">
            <Icon name="headphones" className="text-xl text-teal-flow" />
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

          {/* Kiểm tra validate thời lượng audio vs khoảng trống chặng */}
          {preview.narration.durationSeconds > maxAudioSeconds ? (
            <Notice tone="error">
              ⚠️ <strong>Audio quá dài ({preview.narration.durationSeconds} giây)!</strong> Vượt quá khoảng trống tối đa cho phép là <strong>{maxAudioSeconds} giây</strong> (chặng di chuyển {segmentDuration}s). Nếu xuất bản, audio sẽ bị câu thông báo &ldquo;Sắp đến bến&rdquo; đè lên. Vui lòng bấm &ldquo;Dùng gợi ý này&rdquo; hoặc rút ngắn kịch bản để được duyệt.
            </Notice>
          ) : (
            <div className="flex items-center gap-1.5 text-xs text-teal-900 font-semibold bg-teal-50 border border-teal-200 p-2.5 rounded-lg">
              <span>✅</span>
              <span>
                Thời lượng audio {preview.narration.durationSeconds}s nằm vừa vặn trong khoảng trống an toàn ({maxAudioSeconds}s). Sẵn sàng xuất bản lên tàu.
              </span>
            </div>
          )}

          {preview.text !== text && (
            <p className="text-xs text-coral-glow">{t('The script changed after this audio. Generate again to hear it.')}</p>
          )}
          {publishing.error && <Notice tone="error">{publishing.error}</Notice>}
          {published && <Notice tone="success">{t('Audio guide published.')}</Notice>}
          <Button size="sm"
            icon="publish"
            disabled={publishing.busy || published || preview.text !== text || preview.narration.durationSeconds > maxAudioSeconds}
            onClick={publish}
            className="w-full"
          >
            {publishing.busy ? t('Publishing…') : preview.narration.durationSeconds > maxAudioSeconds ? 'Không thể xuất bản (Audio quá dài)' : t('Publish this audio')}
          </Button>
        </div>
      )}
    </form>
  )
}

export default function AudioGuidesPage() {
  useDocumentTitle('Thuyết minh & Âm thanh')
  const [activeTab, setActiveTab] = useState<'pois' | 'stations' | 'config'>('pois')
  const [version, reload] = useReload()
  const pois = useFetch(getNarrationPois, [version])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [previews, setPreviews] = useState<Record<string, Preview>>({})

  // Cấu hình thời gian toàn chuyến từ bộ mô phỏng (từ bến đầu đến bến cuối)
  const [simConfig, setSimConfig] = useState<StopAnnouncementConfig | null>(null)

  useEffect(() => {
    getSimulatorConfig().then(setSimConfig).catch(() => {})
  }, [])

  // Ngân sách thời gian do BE dựng là nguồn giới hạn duy nhất cho độ dài thuyết minh.
  const budget = simConfig?.budget
  const maxAudioSeconds = budget ? Math.max(1, Math.floor(budget.maxNarrationSeconds)) : 1
  const maxCharacters = budget ? Math.max(1, budget.maxNarrationCharacters) : 1
  const segmentDuration = budget ? Math.round(budget.segmentMovingSeconds) : 0

  // Object URLs keep the MP3 in memory: release them when the page closes.
  const urls = useRef<string[]>([])
  useEffect(() => () => urls.current.forEach((url) => URL.revokeObjectURL(url)), [])

  if (pois.error) return <ErrorState onRetry={pois.retry} />
  if (!pois.data) return <PageLoader />

  const selected = pois.data.find((p) => p.poiId === selectedId) ?? pois.data[0]

  const savePreview = (poiId: string, preview: Preview) => {
    setPreviews((current) => {
      const old = current[poiId]
      if (old) URL.revokeObjectURL(old.url)
      return { ...current, [poiId]: preview }
    })
    urls.current.push(preview.url)
  }

  const TABS = [
    { id: 'pois', label: t('Thuyết minh điểm du lịch (Sightseeing)') },
    { id: 'stations', label: t('Kịch bản thông báo trạm bến') },
    { id: 'config', label: t('Cấu hình thời gian & Vận hành') },
  ] as const

  const renderTabIcon = (id: 'pois' | 'stations' | 'config') => {
    if (id === 'pois') {
      return (
        <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
      )
    }
    if (id === 'stations') {
      return (
        <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z" />
        </svg>
      )
    }
    return (
      <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
      </svg>
    )
  }

  return (
    <>
      <PageHeader
        title="Thuyết minh & Âm thanh hành trình"
        description="Quản lý bài thuyết minh du lịch Azure AI và các mẫu câu thông báo trạm bến tự động trên tàu."
      />

      {/* Thanh chuyển Tab nghiệp vụ */}
      <div className="mb-6 flex flex-wrap gap-2 border-b border-outline-variant/40 pb-3">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm font-bold transition-all ${
              activeTab === tab.id
                ? 'bg-deep-river text-white shadow-sm'
                : 'bg-white text-deep-river hover:bg-surface-container-low border border-outline-variant/60'
            }`}
          >
            {renderTabIcon(tab.id)}
            <span>{tab.label}</span>
          </button>
        ))}
      </div>

      {activeTab === 'stations' && <StationAnnouncementsTab />}
      {activeTab === 'config' && <SimulatorConfigTab onConfigUpdated={setSimConfig} />}

      {activeTab === 'pois' && (
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
                    <Icon name={poi.isPublished ? 'check_circle' : previews[poi.poiId] ? 'headphones' : 'photo_camera'} className="text-lg" />
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
                maxAudioSeconds={maxAudioSeconds}
                maxCharacters={maxCharacters}
                segmentDuration={segmentDuration}
                onDraft={(draft) => setDrafts((d) => ({ ...d, [selected.poiId]: draft }))}
                onPreview={(preview) => savePreview(selected.poiId, preview)}
                onPublished={reload}
              />
            ) : (
              <div className="flex min-h-[240px] flex-col items-center justify-center gap-2 text-center text-sm text-on-surface-variant">
                <Icon name="touch_app" className="text-3xl text-teal-flow" />
                {t('Pick a landmark to write its narration.')}
              </div>
            )}
          </Panel>
        </div>
      )}
    </>
  )
}
