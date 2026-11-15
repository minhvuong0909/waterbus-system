import { useEffect, useState } from 'react'
import { Button, Notice, Panel } from '../../../components/ui'
import { getStations } from '../services/stationService'
import type { AdminStation } from '../types'
import { NARRATION_VOICES } from '../utils/audioGuide'
import type { NarrationVoice } from '../utils/audioGuide'
import { fetchNarrationAudio, generateNarration } from '../services/audioGuideService'
import { t } from '../../../i18n'

interface TemplateItem {
  id: string
  title: string
  badge: string
  badgeColor: string
  timing: string
  defaultText: string
}

interface TemplateSegment {
  id: string
  type: 'text' | 'variable'
  value: string
  label?: string
}

const TEMPLATES: TemplateItem[] = [
  {
    id: 'departing',
    title: '1. Thông báo rời bến',
    badge: 'Rời bến',
    badgeColor: 'bg-blue-100 text-blue-800 border-blue-200',
    timing: 'Tự động phát khi tàu nhổ neo xuất bến',
    defaultText: 'Tàu đang rời bến {Bến hiện tại}, điểm đến tiếp theo: Bến {Bến kế tiếp}.',
  },
  {
    id: 'near',
    title: '2. Thông báo sắp đến bến',
    badge: 'Gần tới bến',
    badgeColor: 'bg-amber-100 text-amber-800 border-amber-200',
    timing: 'Tự động phát khi tàu chạy được 80% chặng đường',
    defaultText: 'Tàu sắp đến bến {Bến kế tiếp}. Quý khách vui lòng chuẩn bị tư trang hành lý.',
  },
  {
    id: 'arrived_mid',
    title: '3. Thông báo cập bến trung gian',
    badge: 'Dừng đón trả khách',
    badgeColor: 'bg-emerald-100 text-emerald-800 border-emerald-200',
    timing: 'Tự động phát khi cập cầu tàu giữa tuyến (dừng đón khách rồi đi tiếp)',
    defaultText: 'Tàu đã đến bến {Bến hiện tại}. Tàu sẽ dừng 3 giây để quý khách lên xuống.',
  },
  {
    id: 'arrived_final',
    title: '4. Thông báo cập bến cuối cùng',
    badge: 'Kết thúc chuyến',
    badgeColor: 'bg-rose-100 text-rose-800 border-rose-200',
    timing: 'Tự động phát khi cập bến cuối cùng của chuyến đi (hành khách rời tàu)',
    defaultText: 'Tàu đã đến bến {Bến cuối}. Đây là điểm dừng cuối cùng của chuyến đi, quý khách vui lòng kiểm tra hành lý và rời tàu.',
  },
]

function parseTemplateToSegments(templateStr: string): TemplateSegment[] {
  const regex = /(\{[^}]+\})/g
  const parts = templateStr.split(regex)
  return parts
    .filter((part) => part.length > 0)
    .map((part, index) => {
      if (part.startsWith('{') && part.endsWith('}')) {
        let label = 'Bến tự động'
        if (part === '{Bến hiện tại}') label = 'Bến hiện tại'
        if (part === '{Bến kế tiếp}') label = 'Bến kế tiếp'
        if (part === '{Bến cuối}') label = 'Bến cuối'
        return {
          id: `var-${index}`,
          type: 'variable',
          value: part,
          label,
        }
      }
      return {
        id: `txt-${index}`,
        type: 'text',
        value: part,
      }
    })
}

export default function StationAnnouncementsTab() {
  const [stations, setStations] = useState<AdminStation[]>([])
  const [currentStationName, setCurrentStationName] = useState<string>('Bạch Đằng')
  const [nextStationName, setNextStationName] = useState<string>('Thủ Thiêm')
  const [voice, setVoice] = useState<NarrationVoice>(NARRATION_VOICES[0].id)
  const [playingId, setPlayingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Lưu trữ câu sửa đổi của admin (nếu có)
  const [customTexts, setCustomTexts] = useState<Record<string, string>>(() => {
    try {
      const saved = localStorage.getItem('waterbus_announcement_custom_texts')
      return saved ? JSON.parse(saved) : {}
    } catch {
      return {}
    }
  })

  // Đang mở ô chỉnh sửa cho mẫu câu nào
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingSegments, setEditingSegments] = useState<TemplateSegment[]>([])

  useEffect(() => {
    getStations().then((data) => {
      const active = data.filter((s) => s.isActive).sort((a, b) => a.orderIndex - b.orderIndex)
      setStations(active)
      if (active.length >= 2) {
        setCurrentStationName(active[0].name)
        setNextStationName(active[1].name)
      }
    })
  }, [])

  const getTemplateText = (tpl: TemplateItem) => customTexts[tpl.id] || tpl.defaultText

  const buildSampleSentence = (tpl: TemplateItem) => {
    const raw = getTemplateText(tpl)
    return raw
      .replace(/{Bến hiện tại}/g, currentStationName)
      .replace(/{Bến kế tiếp}/g, nextStationName)
      .replace(/{Bến cuối}/g, nextStationName)
  }

  const handleStartEdit = (tpl: TemplateItem) => {
    setEditingId(tpl.id)
    setEditingSegments(parseTemplateToSegments(getTemplateText(tpl)))
  }

  const handleSegmentTextChange = (id: string, newText: string) => {
    setEditingSegments((prev) =>
      prev.map((seg) => (seg.id === id ? { ...seg, value: newText } : seg))
    )
  }

  const handleSaveEdit = (tplId: string) => {
    const fullText = editingSegments.map((s) => s.value).join('')
    const updated = { ...customTexts, [tplId]: fullText }
    setCustomTexts(updated)
    setEditingId(null)
    setEditingSegments([])
    try {
      localStorage.setItem('waterbus_announcement_custom_texts', JSON.stringify(updated))
    } catch (e) {
      console.error(e)
    }
  }

  const handlePlayAzureTts = async (tpl: TemplateItem) => {
    setError(null)
    setPlayingId(tpl.id)
    const sentence = buildSampleSentence(tpl)
    try {
      const generated = await generateNarration(sentence, voice)
      const blob = await fetchNarrationAudio(generated.assetKey)
      const audioUrl = URL.createObjectURL(blob)
      const audio = new Audio(audioUrl)
      audio.onended = () => {
        setPlayingId(null)
        URL.revokeObjectURL(audioUrl)
      }
      audio.onerror = () => {
        setPlayingId(null)
        setError(t('Không thể phát âm thanh Azure'))
      }
      await audio.play()
    } catch (err: unknown) {
      setPlayingId(null)
      setError(err instanceof Error ? err.message : t('Lỗi khi gọi Azure Speech'))
    }
  }

  return (
    <div className="space-y-5">
      {/* Thanh công cụ xem thử gọn gàng */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-surface-container-low p-4 border border-outline-variant/40">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-deep-river">{t('Bến vừa đi:')}</span>
            <select
              value={currentStationName}
              onChange={(e) => setCurrentStationName(e.target.value)}
              className="rounded-lg border border-outline-variant bg-white px-2.5 py-1 text-xs font-medium text-deep-river"
            >
              {stations.map((s) => (
                <option key={s.id} value={s.name}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-deep-river">{t('Bến sắp đến:')}</span>
            <select
              value={nextStationName}
              onChange={(e) => setNextStationName(e.target.value)}
              className="rounded-lg border border-outline-variant bg-white px-2.5 py-1 text-xs font-medium text-deep-river"
            >
              {stations.map((s) => (
                <option key={s.id} value={s.name}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-deep-river">{t('Giọng đọc:')}</span>
          <select
            value={voice}
            onChange={(e) => setVoice(e.target.value as NarrationVoice)}
            className="rounded-lg border border-outline-variant bg-white px-2.5 py-1 text-xs font-medium text-deep-river"
          >
            {NARRATION_VOICES.map((v) => (
              <option key={v.id} value={v.id}>
                {t(v.label)}
              </option>
            ))}
          </select>
        </div>
      </div>

      {error && <Notice tone="error">{error}</Notice>}

      {/* Danh sách 4 Mẫu câu */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {TEMPLATES.map((tpl) => {
          const isEditing = editingId === tpl.id
          const segments = parseTemplateToSegments(getTemplateText(tpl))

          return (
            <Panel key={tpl.id} className="space-y-3 flex flex-col justify-between">
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-2 border-b border-outline-variant/30 pb-2">
                  <h3 className="font-bold text-sm text-deep-river">{tpl.title}</h3>
                  <span className={`rounded-md border px-2 py-0.5 text-2xs font-semibold ${tpl.badgeColor}`}>
                    {tpl.badge}
                  </span>
                </div>

                <p className="text-2xs text-on-surface-variant italic">{tpl.timing}</p>

                {isEditing ? (
                  <div className="space-y-3 rounded-xl bg-surface-container-low/60 p-3.5 border border-outline-variant/60">
                    <div className="flex items-center justify-between text-2xs">
                      <span className="font-semibold text-deep-river">Chỉnh sửa lời dẫn:</span>
                      <span className="text-teal-700 font-medium">🔒 Tên bến màu xanh được khóa cố định</span>
                    </div>

                    <div className="flex flex-col gap-2">
                      {editingSegments.map((seg) => {
                        if (seg.type === 'variable') {
                          return (
                            <div
                              key={seg.id}
                              className="flex items-center justify-between gap-2 rounded-lg bg-teal-50 px-3 py-2 border border-teal-300 shadow-2xs select-none"
                              title="Biến hệ thống: Tự động điền theo chuyến đi thực tế. Không thể chỉnh sửa hoặc xóa."
                            >
                              <div className="flex items-center gap-2">
                                <span className="text-xs">🔒</span>
                                <span className="font-bold text-xs text-teal-950">{seg.value}</span>
                                <span className="text-3xs font-medium text-teal-800 bg-teal-200/70 px-2 py-0.5 rounded-full">
                                  {seg.label}
                                </span>
                              </div>
                              <span className="text-3xs font-semibold uppercase tracking-wider text-teal-700">
                                Cố định • Không thể xoá
                              </span>
                            </div>
                          )
                        }

                        return (
                          <div key={seg.id}>
                            <input
                              type="text"
                              value={seg.value}
                              onChange={(e) => handleSegmentTextChange(seg.id, e.target.value)}
                              placeholder="Nhập lời dẫn câu chữ..."
                              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-1.5 text-xs font-medium text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
                            />
                          </div>
                        )
                      })}
                    </div>

                    <div className="flex items-center justify-end gap-2 pt-1 border-t border-outline-variant/30">
                      <Button size="sm" variant="neutral" onClick={() => setEditingId(null)}>
                        {t('Hủy')}
                      </Button>
                      <Button size="sm" variant="primary" onClick={() => handleSaveEdit(tpl.id)}>
                        {t('Lưu kịch bản')}
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div className="rounded-xl bg-surface-container-low p-3.5 border border-outline-variant/40 leading-relaxed text-sm text-deep-river">
                    &ldquo;
                    {segments.map((seg) => {
                      if (seg.type === 'variable') {
                        let name = currentStationName
                        if (seg.value === '{Bến kế tiếp}' || seg.value === '{Bến cuối}') {
                          name = nextStationName
                        }
                        return (
                          <span
                            key={seg.id}
                            className="inline-flex items-center gap-1 mx-1 px-2.5 py-0.5 rounded-md bg-teal-100 text-teal-900 border border-teal-300 font-bold text-xs shadow-2xs"
                            title={`${seg.value} (Tự động thay bằng tên bến thực tế khi tàu chạy)`}
                          >
                            <span className="text-xs">📍</span>
                            <span>{name}</span>
                          </span>
                        )
                      }
                      return <span key={seg.id}>{seg.value}</span>
                    })}
                    &rdquo;
                  </div>
                )}
              </div>

              {!isEditing && (
                <div className="flex items-center justify-between pt-2 border-t border-outline-variant/20">
                  <button
                    type="button"
                    onClick={() => handleStartEdit(tpl)}
                    className="text-xs text-teal-flow hover:underline font-semibold"
                  >
                    {t('Chỉnh sửa')}
                  </button>

                  <Button
                    size="sm"
                    variant="primary"
                    icon={playingId === tpl.id ? 'graphic_eq' : 'volume_up'}
                    disabled={playingId !== null}
                    onClick={() => handlePlayAzureTts(tpl)}
                  >
                    {playingId === tpl.id ? t('Đang phát…') : t('Nghe thử Azure AI')}
                  </Button>
                </div>
              )}
            </Panel>
          )
        })}
      </div>
    </div>
  )
}
