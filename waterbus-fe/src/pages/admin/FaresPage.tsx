import { useState } from 'react'
import { Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, Notice, PageHeader, PageLoader, SearchSelect } from '../../components/ui'
import type { Column } from '../../components/ui'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import FareRuleForm from '../../features/admin/components/FareRuleForm'
import StatusBadge from '../../features/admin/components/StatusBadge'
import type { BadgeTone } from '../../features/admin/components/StatusBadge'
import { deactivateFareRule, getFareRules } from '../../features/admin/services/fareRuleService'
import { getSeatClasses } from '../../features/admin/services/seatClassService'
import type { FareRule } from '../../features/admin/types'
import { combineFetches, useDocumentTitle, useFetch, useReload } from '../../hooks'
import { t } from '../../i18n'
import { formatVnd } from '../../utils/format'
import { formatClock, formatLongDate } from '../../utils/time'

type Dialog = { kind: 'form'; rule?: FareRule } | { kind: 'deactivate'; rule: FareRule } | null

const STATUS_BADGE: Record<FareRule['status'], { tone: BadgeTone; label: string }> = {
  Active: { tone: 'active', label: 'In effect' },
  Upcoming: { tone: 'upcoming', label: 'Upcoming' },
  Expired: { tone: 'muted', label: 'Expired' },
  Inactive: { tone: 'muted', label: 'Switched off' },
}

/** Giá vé áp dụng nhiều tháng nên ngày bắt đầu và kết thúc hiện kèm năm. */
const moment = (iso: string) => `${formatLongDate(iso)}, ${formatClock(new Date(iso))}`

const tripTypeLabel = (rule: FareRule) => t(rule.tripType === 'Commuter' ? 'Regular' : 'Sightseeing')

/** Quy tắc giá (BR-FARE-01): một giá cho mỗi loại chuyến và hạng ghế, đi bến nào cũng vậy. */
export default function FaresPage() {
  useDocumentTitle('Fares')
  const [version, reload] = useReload()
  const rules = useFetch(getFareRules, [version])
  const seatClasses = useFetch(() => getSeatClasses(true))
  const [dialog, setDialog] = useState<Dialog>(null)
  const [tripType, setTripType] = useState('')
  const [seatClassId, setSeatClassId] = useState('')
  const [status, setStatus] = useState('')

  const all = combineFetches(rules, seatClasses)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!rules.data || !seatClasses.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  const filteredRules = rules.data.filter(
    (f) =>
      (tripType === '' || f.tripType === tripType) &&
      (seatClassId === '' || f.seatClassId === seatClassId) &&
      (status === '' || f.status === status),
  )
  // Xây từ CHÍNH dữ liệu giá vé hiện có (không phải `seatClasses.data`, vốn chỉ chứa hạng ghế đang
  // hoạt động) - để lọc được cả theo hạng ghế đã ngừng nếu vẫn còn giá vé cũ gắn với hạng đó.
  const seatClassOptions = Array.from(
    new Map(rules.data.map((f) => [f.seatClassId, f.seatClassName])).entries(),
  ).sort((a, b) => a[1].localeCompare(b[1]))

  // Hạng ghế đang hoạt động chưa có giá đang áp dụng cho một loại chuyến thì không bán được (tìm chuyến và sơ đồ ghế ẩn).
  const allRules = rules.data
  const activeClasses = seatClasses.data
  const unpriced = (['Commuter', 'Sightseeing'] as const).flatMap((type) =>
    activeClasses
      .filter((c) => !allRules.some((f) => f.tripType === type && f.seatClassId === c.id && f.status === 'Active'))
      .map((c) => `${t(type === 'Commuter' ? 'Regular' : 'Sightseeing')} · ${c.name}`),
  )

  const columns: Column<FareRule>[] = [
    { header: 'Trip type', render: tripTypeLabel },
    { header: 'Seat class', render: (f) => f.seatClassName, className: 'whitespace-nowrap' },
    { header: 'Price', render: (f) => formatVnd(f.price), className: 'whitespace-nowrap text-right' },
    { header: 'Starts', render: (f) => moment(f.effectiveFrom), className: 'whitespace-nowrap' },
    {
      header: 'Ends',
      className: 'whitespace-nowrap',
      render: (f) =>
        f.effectiveTo ? moment(f.effectiveTo) : <span className="text-on-surface-variant">{t('No end')}</span>,
    },
    { header: 'Status', render: (f) => <StatusBadge {...STATUS_BADGE[f.status]} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (f) =>
        f.isActive && f.status !== 'Expired' && (
          <span className="inline-flex gap-1">
            <Button size="sm" variant="neutral" icon="edit" onClick={() => setDialog({ kind: 'form', rule: f })}>
              {t('Edit')}
            </Button>
            <Button size="sm" variant="neutral" onClick={() => setDialog({ kind: 'deactivate', rule: f })}>
              {t('Switch off')}
            </Button>
          </span>
        ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Fares"
        description="One price per trip type and seat class, the same whichever piers are travelled."
        actions={
          <Button
            size="sm"
            icon="add"
            disabled={seatClasses.data.length === 0}
            onClick={() => setDialog({ kind: 'form' })}
          >
            {t('Add fare')}
          </Button>
        }
      />
      {unpriced.length > 0 && (
        <div className="mb-3">
          <Notice tone="warning">
            {t('No fare in effect for: {list}. These cannot be sold until a fare is added.', { list: unpriced.join('; ') })}
          </Notice>
        </div>
      )}
      <FilterBar>
        <FilterChips label="Trip type" value={tripType} onChange={setTripType} options={[
          { value: '', label: 'All types' }, { value: 'Commuter', label: 'Regular' }, { value: 'Sightseeing', label: 'Sightseeing' },
        ]} />
        <SearchSelect label="Seat class" value={seatClassId} onChange={setSeatClassId} options={[
          { value: '', label: t('All') }, ...seatClassOptions.map(([value, label]) => ({ value, label })),
        ]} />
        <FilterChips label="Status" value={status} onChange={setStatus} options={[
          { value: '', label: 'All statuses' }, ...(['Active', 'Upcoming', 'Expired', 'Inactive'] as const).map((value) => ({ value, label: STATUS_BADGE[value].label })),
        ]} />
      </FilterBar>
      <DataTable paginate columns={columns} rows={filteredRules} rowKey={(f) => f.id} empty="No fares match this filter." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.rule ? 'Edit fare' : 'Add fare'} onClose={() => setDialog(null)}>
          <FareRuleForm
            rule={dialog.rule}
            seatClasses={seatClasses.data}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate fare"
          message={t('Deactivate the {type} fare for {seatClass}? It cannot be switched back on; add a new fare instead. If it is the only fare in effect for this seat class, that class can no longer be sold.', {
            type: tripTypeLabel(dialog.rule),
            seatClass: dialog.rule.seatClassName,
          })}
          confirmLabel="Deactivate"
          action={() => deactivateFareRule(dialog.rule.id)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}
