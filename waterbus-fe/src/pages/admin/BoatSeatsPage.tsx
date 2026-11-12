import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Button, ErrorState, Modal, PageHeader, PageLoader, Panel } from '../../components/ui'
import AddSeatsForm from '../../features/admin/components/AddSeatsForm'
import BoatSeatPlan from '../../features/admin/components/BoatSeatPlan'
import SeatForm from '../../features/admin/components/SeatForm'
import StatusBadge from '../../features/admin/components/StatusBadge'
import type { BadgeTone } from '../../features/admin/components/StatusBadge'
import { useReload } from '../../features/admin/hooks/useAdmin'
import { getBoat, getBoatSeats } from '../../features/admin/services/boatSeatService'
import { getSeatClasses } from '../../features/admin/services/seatClassService'
import type { BoatSeat, BoatSeatInventory } from '../../features/admin/types'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { isGuid } from '../../utils/guid'

type Dialog = { kind: 'add' } | { kind: 'seat'; seat: BoatSeat } | null

const STATE_BADGE: Record<BoatSeatInventory['state'], { tone: BadgeTone; label: string }> = {
  NotConfigured: { tone: 'muted', label: 'No seats yet' },
  Partial: { tone: 'upcoming', label: 'Seat plan incomplete' },
  Complete: { tone: 'active', label: 'Seat plan complete' },
}

function BoatSeats({ boatId }: { boatId: string }) {
  const [version, reload] = useReload()
  const boat = useFetch(() => getBoat(boatId), [boatId])
  const inventory = useFetch(() => getBoatSeats(boatId), [boatId, version])
  const seatClasses = useFetch(() => getSeatClasses(true))
  const [dialog, setDialog] = useState<Dialog>(null)

  const all = combineFetches(boat, inventory, seatClasses)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!boat.data || !inventory.data || !seatClasses.data) return <PageLoader />

  const { totalSeats, activeSeats, state, seats } = inventory.data
  const room = totalSeats - activeSeats
  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  return (
    <>
      <PageHeader
        title="Boat seats"
        description={`${boat.data.code} · ${boat.data.name}`}
        actions={
          <>
            <Button size="sm" variant="neutral" icon="arrow_back" to={ROUTES.adminBoats}>
              {t('Boats')}
            </Button>
            <Button
              size="sm"
              icon="add"
              disabled={room <= 0 || seatClasses.data.length === 0}
              onClick={() => setDialog({ kind: 'add' })}
            >
              {t('Add seats')}
            </Button>
          </>
        }
      />

      <Panel>
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2 border-b border-surface-container pb-4">
          <p className="text-sm text-on-surface-variant">
            <span className="font-display text-2xl font-bold text-deep-river">{activeSeats}</span>
            {' / '}
            {t('{total} seats active', { total: totalSeats })}
          </p>
          <StatusBadge {...STATE_BADGE[state]} />
        </div>
        {seats.length === 0 ? (
          <p className="py-6 text-center text-sm text-on-surface-variant">
            {t('This boat has no seats yet. Add its seats so trips on it can be sold.')}
          </p>
        ) : (
          <BoatSeatPlan seats={seats} onOpen={(seat) => setDialog({ kind: 'seat', seat })} />
        )}
      </Panel>

      {dialog?.kind === 'add' && (
        <Modal title="Add seats" onClose={() => setDialog(null)}>
          <AddSeatsForm
            boatId={boatId}
            seatClasses={seatClasses.data}
            room={room}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'seat' && (
        <Modal title="Edit seat" onClose={() => setDialog(null)}>
          <SeatForm
            seat={dialog.seat}
            seatClasses={seatClasses.data}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
    </>
  )
}

/** Seat plan of one boat (`?boatId=`), opened from the Boats page. */
export default function BoatSeatsPage() {
  useDocumentTitle('Boat seats')
  const boatId = useSearchParams()[0].get('boatId')
  if (!boatId || !isGuid(boatId)) {
    return (
      <ErrorState
        icon="sailing"
        title={t('No boat chosen')}
        message={t('Open the seats of a boat from the Boats page.')}
        action={{ label: t('Boats'), to: ROUTES.adminBoats }}
      />
    )
  }
  return <BoatSeats boatId={boatId} />
}
