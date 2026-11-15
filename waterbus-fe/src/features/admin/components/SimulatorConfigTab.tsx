import { useEffect, useState } from 'react'
import { Button, Notice, Panel } from '../../../components/ui'
import { getSimulatorConfig, updateSimulatorConfig } from '../services/simulatorService'
import type { StopAnnouncementConfig } from '../services/simulatorService'

export default function SimulatorConfigTab() {
  const [config, setConfig] = useState<StopAnnouncementConfig>({
    segmentDurationSeconds: 45,
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
    setSaving(true)
    setError(null)
    setSavedSuccess(false)
    try {
      const updated = await updateSimulatorConfig({
        segmentDurationSeconds: Number(config.segmentDurationSeconds) || 45,
        dwellTimeSeconds: Number(config.dwellTimeSeconds) || 5,
        nearThreshold: Number(config.nearThreshold) || 0.8,
        useSimulatedSegmentDuration: true,
      })
      setConfig(updated)
      setSavedSuccess(true)
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
      {/* Panel Cấu hình Tham số */}
      <Panel className="space-y-6">
        <div className="border-b border-outline-variant/30 pb-3">
          <h3 className="font-bold text-base text-deep-river">Tham số vận hành & Phát thanh tự động</h3>
          <p className="text-xs text-on-surface-variant mt-0.5">
            Các thiết lập này điều khiển nhịp chạy của tàu trên radar GPS và thời điểm hệ thống kích hoạt câu phát thanh trạm bến.
          </p>
        </div>

        {error && <Notice tone="error">{error}</Notice>}
        {savedSuccess && (
          <Notice tone="success">
            Đã lưu và cập nhật cấu hình tham số vận hành thành công! Hệ thống đang áp dụng theo thông số mới.
          </Notice>
        )}

        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          {/* Thời gian giữa 2 bến */}
          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Thời gian giữa 2 bến (giây)
            </label>
            <input
              type="number"
              min={5}
              max={600}
              value={config.segmentDurationSeconds}
              onChange={(e) => {
                setConfig({ ...config, segmentDurationSeconds: Number(e.target.value) })
                setSavedSuccess(false)
              }}
              className="w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm font-semibold text-deep-river focus:border-teal-flow focus:ring-1 focus:ring-teal-flow"
            />
            <p className="text-2xs text-on-surface-variant">
              Thời gian tàu di chuyển trên sông giữa 2 trạm dừng liên tiếp (mặc định 45 giây).
            </p>
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
              Thời gian tàu neo đậu tại cầu tàu để đón trả khách trước khi xuất bến (mặc định 5 giây).
            </p>
          </div>

          {/* Ngưỡng phát thông báo sắp tới bến */}
          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-deep-river">
              Ngưỡng báo sắp tới bến (%)
            </label>
            <input
              type="number"
              min={10}
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
              Mặc định 80%: Khi tàu chạy được 80% chặng đường sẽ tự động phát câu thông báo số 2.
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
