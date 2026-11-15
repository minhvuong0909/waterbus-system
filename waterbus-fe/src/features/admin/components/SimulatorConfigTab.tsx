import { useEffect, useState } from 'react'
import { Button, Notice, Panel } from '../../../components/ui'
import { getSimulatorConfig, updateSimulatorConfig } from '../services/simulatorService'
import type { StopAnnouncementConfig } from '../services/simulatorService'

interface SimulatorConfigTabProps {
  onConfigUpdated?: (config: StopAnnouncementConfig) => void
}

export default function SimulatorConfigTab({ onConfigUpdated }: SimulatorConfigTabProps) {
  const [config, setConfig] = useState<StopAnnouncementConfig>({
    segmentDurationSeconds: 180,
    dwellTimeSeconds: 5,
    nearThreshold: 0.8,
    useSimulatedSegmentDuration: true,
  })
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

  const handleSave = async () => {
    if (config.segmentDurationSeconds < 30) {
      setError('Thời lượng toàn chuyến (Bến đầu → Bến cuối) tối thiểu phải từ 30 giây trở lên để kịp phát loa thông báo các chặng.')
      return
    }

    setSaving(true)
    setError(null)
    setSavedSuccess(false)
    try {
      const updated = await updateSimulatorConfig({
        segmentDurationSeconds: Number(config.segmentDurationSeconds) || 180,
        dwellTimeSeconds: Number(config.dwellTimeSeconds) || 5,
        nearThreshold: Number(config.nearThreshold) || 0.8,
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

  // Với tuyến 5 bến tiêu chuẩn (4 chặng di chuyển), thời gian mỗi chặng = tổng thời lượng chuyến / 4
  const estimatedSegments = 4
  const segDuration = Math.max(5, Math.round(config.segmentDurationSeconds / estimatedSegments))
  const maxAudioSec = Math.max(5, Math.floor(segDuration * config.nearThreshold - 5))
  const maxCharLimit = maxAudioSec * 14

  return (
    <div className="max-w-4xl space-y-5">
      {/* Panel Cấu hình Tham số */}
      <Panel className="space-y-6">
        <div className="border-b border-outline-variant/30 pb-3">
          <h3 className="font-bold text-base text-deep-river">Tham số vận hành & Thời lượng toàn chuyến đi</h3>
          <p className="text-xs text-on-surface-variant mt-0.5">
            Áp dụng cho toàn bộ chuyến đi từ Bến đầu (xuất phát) đến Bến cuối (kết thúc). Hệ thống tự động phân bổ đều thời gian cho các chặng giữa các bến để tính toán khoảng trống phát thanh an toàn.
          </p>
        </div>

        {error && <Notice tone="error">{error}</Notice>}
        {savedSuccess && (
          <Notice tone="success">
            Đã lưu và cập nhật cấu hình tham số vận hành thành công! Giới hạn độ dài kịch bản ở Tab Thuyết minh đã được đồng bộ theo toàn chuyến.
          </Notice>
        )}

        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          {/* Thời lượng toàn chuyến */}
          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Thời lượng toàn chuyến (Bến đầu → Bến cuối)
            </label>
            <div className="relative">
              <input
                type="number"
                min={30}
                max={3600}
                value={config.segmentDurationSeconds}
                onChange={(e) => {
                  setConfig({ ...config, segmentDurationSeconds: Math.max(1, Number(e.target.value)) })
                  setSavedSuccess(false)
                  setError(null)
                }}
                className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 pr-12 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
              />
              <span className="absolute right-3 top-2.5 text-xs text-on-surface-variant">giây</span>
            </div>
            <div className="rounded-lg bg-teal-50/70 border border-teal-200/60 p-2.5 text-2xs text-teal-900 space-y-1">
              <div className="font-bold">✨ Phân bổ tuyến 5 bến (4 chặng):</div>
              <div>Cả chuyến: <span className="font-bold text-teal-800">{config.segmentDurationSeconds}s (~{(config.segmentDurationSeconds / 60).toFixed(1)} phút)</span></div>
              <div>Mỗi chặng giữa 2 bến: <span className="font-bold text-teal-800">~{segDuration} giây</span></div>
              <div>Khoảng trống thuyết minh/chặng: <span className="font-bold text-teal-700">~{maxAudioSec}s (~{maxCharLimit} ký tự)</span></div>
            </div>
          </div>

          {/* Thời gian dừng tại bến */}
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
              }}
              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
            />
            <p className="text-2xs text-on-surface-variant">
              Thời gian tàu neo đậu tại cầu tàu mỗi bến để đón trả khách trước khi xuất bến (mặc định 5 giây).
            </p>
          </div>

          {/* Ngưỡng phát thông báo sắp tới bến */}
          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Ngưỡng báo sắp tới bến (%)
            </label>
            <input
              type="number"
              min={50}
              max={95}
              value={Math.round(config.nearThreshold * 100)}
              onChange={(e) => {
                const val = Number(e.target.value) / 100
                setConfig({ ...config, nearThreshold: val })
                setSavedSuccess(false)
              }}
              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
            />
            <p className="text-2xs text-on-surface-variant">
              Tỷ lệ chặng đường sẽ kích hoạt câu thông báo số 2: &ldquo;Tàu sắp đến bến&rdquo; (mặc định 80%).
            </p>
          </div>
        </div>

        {/* Nút Lưu cấu hình */}
        <div className="flex items-center justify-end gap-3 pt-3 border-t border-outline-variant/30">
          <Button
            size="md"
            variant="primary"
            disabled={saving}
            onClick={handleSave}
          >
            {saving ? 'Đang lưu cấu hình...' : 'Lưu cấu hình hệ thống'}
          </Button>
        </div>
      </Panel>
    </div>
  )
}
