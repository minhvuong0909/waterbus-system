import { useState } from 'react'
import { Button, DataTable, ErrorState, Modal, PageHeader, PageLoader } from '../../components/ui'
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

  const all = combineFetches(rules, seatClasses)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!rules.data || !seatClasses.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

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
      <DataTable columns={columns} rows={rules.data} rowKey={(f) => f.id} empty="No fares yet." />

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
