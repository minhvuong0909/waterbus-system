import { useState } from 'react'
import { Button, DataTable, ErrorState, Modal, Notice, PageHeader, PageLoader } from '../../components/ui'
import type { Column } from '../../components/ui'
import ActiveToggle from '../../features/admin/components/ActiveToggle'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import SeatClassForm from '../../features/admin/components/SeatClassForm'
import { ActiveBadge } from '../../features/admin/components/StatusBadge'
import { getSeatClasses, setSeatClassActive } from '../../features/admin/services/seatClassService'
import type { SeatClass } from '../../features/admin/types'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'

type Dialog = { kind: 'form'; seatClass?: SeatClass } | { kind: 'deactivate'; seatClass: SeatClass } | null

/** Seat classes: fares are set per trip type and seat class (BR-FARE-01). */
export default function SeatClassesPage() {
  useDocumentTitle('Seat classes')
  const [version, reload] = useReload()
  const classes = useFetch(() => getSeatClasses(), [version])
  const [dialog, setDialog] = useState<Dialog>(null)
  const activate = useSubmit()

  if (classes.error) return <ErrorState onRetry={classes.retry} />
  if (classes.loading || !classes.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  const columns: Column<SeatClass>[] = [
    { header: 'Code', render: (c) => <span className="font-mono font-semibold">{c.code}</span> },
    { header: 'Name', render: (c) => c.name },
    {
      header: 'Description',
      render: (c) => <span className="text-on-surface-variant">{c.description ?? '—'}</span>,
    },
    { header: 'Seats', render: (c) => c.seatCount, className: 'text-right' },
    { header: 'Status', render: (c) => <ActiveBadge active={c.isActive} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (c) => (
        <span className="inline-flex gap-1">
          <Button size="sm" variant="neutral" icon="edit" onClick={() => setDialog({ kind: 'form', seatClass: c })}>
            {t('Edit')}
          </Button>
          <ActiveToggle
            active={c.isActive}
            busy={activate.busy}
            onDeactivate={() => setDialog({ kind: 'deactivate', seatClass: c })}
            onActivate={async () => {
              if (await activate.run(() => setSeatClassActive(c.id, true).then(() => true))) reload()
            }}
          />
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Seat classes"
        description="Cabin classes of the fleet. Each seat belongs to one class, and fares are set per class."
        actions={
          <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add seat class')}
          </Button>
        }
      />
      {activate.error && (
        <div className="mb-3">
          <Notice tone="error">{activate.error}</Notice>
        </div>
      )}
      <DataTable columns={columns} rows={classes.data} rowKey={(c) => c.id} empty="No seat classes yet." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.seatClass ? 'Edit seat class' : 'Add seat class'} onClose={() => setDialog(null)}>
          <SeatClassForm seatClass={dialog.seatClass} onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}
      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate seat class"
          message={t('Deactivate {name}? New seats and fares can no longer use this class.', {
            name: dialog.seatClass.name,
          })}
          confirmLabel="Deactivate"
          action={() => setSeatClassActive(dialog.seatClass.id, false)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}
