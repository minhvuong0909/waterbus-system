import { useState } from 'react'
import { Button, DataTable, ErrorState, Modal, Notice, PageHeader, PageLoader } from '../../components/ui'
import type { Column } from '../../components/ui'
import ActiveToggle from '../../features/admin/components/ActiveToggle'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import GenerateTripsForm from '../../features/admin/components/GenerateTripsForm'
import ScheduleForm from '../../features/admin/components/ScheduleForm'
import { ActiveBadge } from '../../features/admin/components/StatusBadge'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import { daysLabel } from '../../features/admin/scheduleDays'
import { getRoutes } from '../../features/admin/services/routeService'
import { getSchedule, getSchedules, setScheduleActive } from '../../features/admin/services/scheduleService'
import type { ScheduleDetail, ScheduleListItem } from '../../features/admin/types'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatLongDate } from '../../utils/time'

type Dialog =
  | { kind: 'form'; schedule?: ScheduleDetail }
  | { kind: 'generate'; schedule: ScheduleListItem }
  | { kind: 'deactivate'; schedule: ScheduleListItem }
  | null

/**
 * The days a schedule is valid for. A schedule saved without a start carries the backend's
 * minimum date (year 1), which is shown as having no start rather than as a date.
 */
function validity({ effectiveFrom, effectiveTo }: ScheduleListItem): string {
  const to = effectiveTo ? formatLongDate(effectiveTo) : null
  if (effectiveFrom.startsWith('0001')) return to ? t('Until {date}', { date: to }) : t('No end date')
  const from = formatLongDate(effectiveFrom)
  return to ? `${from} – ${to}` : t('From {date}', { date: from })
}

/** Schedules: when a route runs. Trips are created from a schedule for a range of days. */
export default function SchedulesPage() {
  useDocumentTitle('Schedules')
  const [version, reload] = useReload()
  const schedules = useFetch(getSchedules, [version])
  const routes = useFetch(() => getRoutes(true))
  const [dialog, setDialog] = useState<Dialog>(null)
  const action = useSubmit()

  const all = combineFetches(schedules, routes)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!schedules.data || !routes.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  // The list has no stops, so the editor opens on the full schedule.
  const edit = async (id: string) => {
    const schedule = await action.run(() => getSchedule(id))
    if (schedule) setDialog({ kind: 'form', schedule })
  }

  const columns: Column<ScheduleListItem>[] = [
    {
      header: 'Departs',
      render: (s) => <span className="font-display text-base font-bold">{s.departureTime.slice(0, 5)}</span>,
    },
    {
      header: 'Route',
      className: 'min-w-[13rem]',
      render: (s) => (
        <>
          <div className="font-semibold">{s.routeName}</div>
          <div className="text-xs text-on-surface-variant">
            <span className="font-mono">{s.routeCode}</span> · {t(s.direction)} ·{' '}
            {t('{count} stops', { count: s.stopCount })}
          </div>
        </>
      ),
    },
    { header: 'Days', render: (s) => daysLabel(s.daysOfWeek), className: 'whitespace-nowrap' },
    {
      header: 'Valid',
      className: 'whitespace-nowrap',
      render: validity,
    },
    { header: 'Status', render: (s) => <ActiveBadge active={s.isActive} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (s) => (
        <span className="inline-flex gap-1">
          {s.isActive && (
            <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: 'generate', schedule: s })}>
              {t('Create trips')}
            </Button>
          )}
          <Button size="sm" variant="neutral" icon="edit" disabled={action.busy} onClick={() => edit(s.id)}>
            {t('Edit')}
          </Button>
          <ActiveToggle
            active={s.isActive}
            busy={action.busy}
            onDeactivate={() => setDialog({ kind: 'deactivate', schedule: s })}
            onActivate={async () => {
              if (await action.run(() => setScheduleActive(s.id, true).then(() => true))) reload()
            }}
          />
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Schedules"
        description="When each route runs. Create the trips of a schedule for the days you want to sell."
        actions={
          <Button
            size="sm"
            icon="add"
            disabled={routes.data.length === 0}
            onClick={() => setDialog({ kind: 'form' })}
          >
            {t('Add schedule')}
          </Button>
        }
      />
      {action.error && (
        <div className="mb-3">
          <Notice tone="error">{action.error}</Notice>
        </div>
      )}
      <DataTable columns={columns} rows={schedules.data} rowKey={(s) => s.id} empty="No schedules yet." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.schedule ? 'Edit schedule' : 'Add schedule'} onClose={() => setDialog(null)}>
          <ScheduleForm
            schedule={dialog.schedule}
            routes={routes.data}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'generate' && (
        <Modal title="Create trips" onClose={() => setDialog(null)}>
          <GenerateTripsForm schedule={dialog.schedule} onClose={() => setDialog(null)} />
        </Modal>
      )}
      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate schedule"
          message={t('Deactivate the {time} schedule of {route}? No new trips are created from it; trips already created stay.', {
            time: dialog.schedule.departureTime.slice(0, 5),
            route: dialog.schedule.routeName,
          })}
          confirmLabel="Deactivate"
          action={() => setScheduleActive(dialog.schedule.id, false)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}
