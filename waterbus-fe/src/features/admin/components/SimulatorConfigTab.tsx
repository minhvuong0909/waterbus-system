import { useEffect, useState } from 'react'
import { Button, Notice, Panel } from '../../../components/ui'
import { getSimulatorConfig, updateSimulatorConfig } from '../services/simulatorService'
import type { StopAnnouncementConfig } from '../services/simulatorService'
import { previewTimeBudget } from '../utils/timeBudget'

interface SimulatorConfigTabProps {
  onConfigUpdated?: (config: StopAnnouncementConfig) => void
}

const DEFAULT_CONFIG: StopAnnouncementConfig = {
  tripDurationSeconds: 180,
  dwellTimeSeconds: 6,
  nearThreshold: 0.75,
  useSimulatedSegmentDuration: true,
  budget: {
    segmentCount: 4,
    segmentMovingSeconds: 0,
    departingWindowSeconds: 0,
    nearWindowSeconds: 0,
    dwellSeconds: 6,
    maxNarrationSeconds: 0,
    maxNarrationCharacters: 0,
    minDwellSeconds: 5,
    minTripDurationSeconds: 0,
    isValid: false,
    violations: [],
  },
}

const MIN_TRIP_DURATION = 60
const MAX_TRIP_DURATION = 7200

export default function SimulatorConfigTab({ onConfigUpdated }: SimulatorConfigTabProps) {
  const [config, setConfig] = useState<StopAnnouncementConfig>(DEFAULT_CONFIG)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [savedSuccess, setSavedSuccess] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    getSimulatorConfig()
      .then((data) => {
        if (mounted) {
          setConfig(data)
          setLoading(false)
        }
      })
      .catch((err) => {
        if (mounted) {
          setError(err instanceof Error ? err.message : 'Không thể tải cấu hình mô phỏng')
          setLoading(false)
        }
      })
    return () => {
      mounted = false
    }
  }, [])

  const preview = previewTimeBudget({
    tripDurationSeconds: config.tripDurationSeconds,
    dwellTimeSeconds: config.dwellTimeSeconds,
    nearThreshold: config.nearThreshold,
    segmentCount: config.budget.segmentCount,
  })

  const handleSave = async () => {
    if (config.tripDurationSeconds < MIN_TRIP_DURATION) {
      setError(
        `Thời lượng toàn chuyến (Bến đầu → Bến cuối) tối thiểu phải từ ${MIN_TRIP_DURATION} giây trở lên để kịp phát loa thông báo các chặng.`,
      )
      return
    }
    if (!preview.isValid) {
      setError(preview.violations.map((v) => v.message).join(' '))
      return
    }

    setSaving(true)
    setError(null)
    setSavedSuccess(false)
    try {
      const updated = await updateSimulatorConfig({
        ...config,
        tripDurationSeconds: Number(config.tripDurationSeconds) || DEFAULT_CONFIG.tripDurationSeconds,
        dwellTimeSeconds: Number(config.dwellTimeSeconds) || DEFAULT_CONFIG.dwellTimeSeconds,
        nearThreshold: Number(config.nearThreshold) || DEFAULT_CONFIG.nearThreshold,
        useSimulatedSegmentDuration: true,
      })
      setConfig(updated)
      setSavedSuccess(true)
      onConfigUpdated?.(updated)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Lỗi khi lưu cấu hình')
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-12 text-sm text-on-surface-variant">
        <span>Đang nạp cấu hình hệ thống...</span>
      </div>
    )
  }

  return (
    <div className="max-w-4xl space-y-5">
      <Panel className="space-y-6">
        <div className="border-b border-outline-variant/30 pb-3">
          <h3 className="font-bold text-base text-deep-river">Tham số vận hành & Thời lượng toàn chuyến đi</h3>
          <p className="text-xs text-on-surface-variant mt-0.5">
            Thời lượng toàn chuyến tính từ Bến đầu (xuất phát) đến Bến cuối (kết thúc), đã bao gồm thời gian đậu tại các
            bến. Hệ thống chia đều phần thời gian di chuyển cho các chặng và tự tính khoảng trống an toàn cho từng
            câu thông báo, kể cả bài thuyết minh địa danh.
          </p>
        </div>

        {error && <Notice tone="error">{error}</Notice>}
        {savedSuccess && (
          <Notice tone="success">
            Đã lưu và cập nhật cấu hình tham số vận hành thành công! Giới hạn độ dài kịch bản ở Tab Thuyết minh đã
            được đồng bộ theo toàn chuyến.
          </Notice>
        )}

        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Thời lượng toàn chuyến (Bến đầu → Bến cuối)
            </label>
            <div className="relative">
              <input
                type="number"
                min={MIN_TRIP_DURATION}
                max={MAX_TRIP_DURATION}
                value={config.tripDurationSeconds}
                onChange={(e) => {
                  setConfig({ ...config, tripDurationSeconds: Math.max(1, Number(e.target.value)) })
                  setSavedSuccess(false)
                  setError(null)
                }}
                className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 pr-12 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
              />
              <span className="absolute right-3 top-2.5 text-xs text-on-surface-variant">giây</span>
            </div>
            <p className="text-2xs text-on-surface-variant">
              Tối thiểu <span className="font-semibold text-coral-glow">{Math.ceil(preview.minTripDurationSeconds)}s</span>{' '}
              cho {preview.segmentCount} chặng (đã trừ thời gian đậu).
            </p>
          </div>

          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Thời gian dừng tại bến (giây)
            </label>
            <input
              type="number"
              min={0}
              max={120}
              value={config.dwellTimeSeconds}
              onChange={(e) => {
                setConfig({ ...config, dwellTimeSeconds: Number(e.target.value) })
                setSavedSuccess(false)
                setError(null)
              }}
              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
            />
            <p className="text-2xs text-on-surface-variant">
              Tối thiểu <span className="font-semibold text-coral-glow">{preview.minDwellSeconds}s</span> để phát trọn
              thông báo cập bến.
            </p>
          </div>

          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Ngưỡng báo sắp tới bến (%)
            </label>
            <input
              type="number"
              min={10}
              max={99}
              value={Math.round(config.nearThreshold * 100)}
              onChange={(e) => {
                setConfig({ ...config, nearThreshold: Number(e.target.value) / 100 })
                setSavedSuccess(false)
                setError(null)
              }}
              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
            />
            <p className="text-2xs text-on-surface-variant">
              Tỷ lệ chặng đường kích hoạt câu &ldquo;Tàu sắp đến bến&rdquo;.
            </p>
          </div>
        </div>

        <div
          className={`rounded-lg border p-3 space-y-1.5 ${
            preview.isValid
              ? 'bg-teal-50/70 border-teal-200/60 text-teal-900'
              : 'bg-red-50/80 border-red-200 text-red-900'
          }`}
        >
          <div className="font-bold text-xs">
            {preview.isValid ? '✓ Ngân sách thời gian hợp lệ' : '✕ Cấu hình sẽ bị chặn khi lưu'}
          </div>
          <div className="text-2xs space-y-0.5">
            <div>
              Mỗi chặng di chuyển:{' '}
              <span className="font-bold">{preview.segmentMovingSeconds.toFixed(1)}s</span> · Cửa sổ rời bến:{' '}
              <span className="font-bold">{preview.departingWindowSeconds.toFixed(1)}s</span> (chứa thông báo rời bến +
              thuyết minh địa danh) · Cửa sổ gần bến:{' '}
              <span className="font-bold">{preview.nearWindowSeconds.toFixed(1)}s</span> · Thời gian đậu:{' '}
              <span className="font-bold">{preview.dwellSeconds.toFixed(1)}s</span>
            </div>
            <div>
              Thuyết minh địa danh tối đa:{' '}
              <span className="font-bold">{preview.maxNarrationSeconds.toFixed(1)}s</span> (~{' '}
              <span className="font-bold">{preview.maxNarrationCharacters}</span> ký tự)
            </div>
            {!preview.isValid &&
              preview.violations.map((v) => (
                <div key={v.property} className="font-semibold">
                  • {v.message}
                </div>
              ))}
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 pt-3 border-t border-outline-variant/30">
          <Button size="md" variant="primary" disabled={saving || !preview.isValid} onClick={handleSave}>
            {saving ? 'Đang lưu cấu hình...' : 'Lưu cấu hình hệ thống'}
          </Button>
        </div>
      </Panel>
    </div>
  )
}
