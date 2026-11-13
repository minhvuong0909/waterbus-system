import { useState } from 'react'
import { Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, PageHeader, PageLoader, SearchSelect } from '../../components/ui'
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

/** Fares run for months, so their start and end are shown with the year. */
const moment = (iso: string) => `${formatLongDate(iso)}, ${formatClock(new Date(iso))}`

const tripTypeLabel = (rule: FareRule) => t(rule.tripType === 'Commuter' ? 'Regular' : 'Sightseeing')

/** Fare rules (BR-FARE-01): one price per trip type and seat class, whichever piers are travelled. */
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

  const hasFilters = tripType !== '' || seatClassId !== '' || status !== ''
  const resetFilters = () => {
    setTripType('')
    setSeatClassId('')
    setStatus('')
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
        f.isActive && (
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
      <FilterBar onReset={hasFilters ? resetFilters : undefined}>
        <FilterChips label="Trip type" value={tripType} onChange={setTripType} options={[
          { value: '', label: 'All types' }, { value: 'Commuter', label: 'Regular' }, { value: 'Sightseeing', label: 'Sightseeing' },
        ]} />
        <SearchSelect label="Seat class" value={seatClassId} onChange={setSeatClassId} options={[
          { value: '', label: t('All') }, ...seatClassOptions.map(([value, label]) => ({ value, label })),
        ]} />
        {/* SearchSelect, không phải FilterChips: 5 lựa chọn làm tràn dòng khi chia đều 3 cột với 2 bộ lọc kia. */}
        <SearchSelect label="Status" value={status} onChange={setStatus} options={[
          { value: '', label: t('All statuses') }, ...(['Active', 'Upcoming', 'Expired', 'Inactive'] as const).map((value) => ({ value, label: t(STATUS_BADGE[value].label) })),
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
          message={t('Deactivate the {type} fare for {seatClass}? It cannot be switched back on; add a new fare instead.', {
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
