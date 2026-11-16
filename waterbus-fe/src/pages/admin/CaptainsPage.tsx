import { useState } from 'react'
import type { FormEvent } from 'react'
import {
  Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, PageHeader, PageLoader, Pagination, SelectField, Tabs, TextField,
} from '../../components/ui'
import type { Column } from '../../components/ui'
import AssignCaptainDialog from '../../features/admin/components/AssignCaptainDialog'
import CaptainAlertsPanel from '../../features/admin/components/CaptainAlertsPanel'
import CaptainAuditPanel from '../../features/admin/components/CaptainAuditPanel'
import FormActions from '../../features/admin/components/FormActions'
import ReasonConfirmModal from '../../features/admin/components/ReasonConfirmModal'
import StatusBadge from '../../features/admin/components/StatusBadge'
import type { BadgeTone } from '../../features/admin/components/StatusBadge'
import {
  changeCaptainStatus, createCaptain, listAllCaptains, listCaptains, resendCaptainInvitation, updateCaptain,
} from '../../features/admin/services/personnelService'
import { getBoats } from '../../features/operations/services/fleetService'
import type { AdminBoat } from '../../features/operations/types'
import { CAPTAIN_STATUS } from '../../features/admin/types/personnel'
import type { Captain, CaptainDraft, CaptainStatus } from '../../features/admin/types/personnel'
import { isEmail, isPhone } from '../../features/admin/utils/personnel'
import { useDocumentTitle, useFetch, useSubmit } from '../../hooks'
import { t } from '../../i18n'

type Dialog =
  | { kind: 'form'; captain?: Captain }
  | { kind: 'status'; captain: Captain }
  | { kind: 'invite'; captain: Captain }
  | { kind: 'boat'; captain: Captain }
  | null

const STATUS_LABEL: Record<CaptainStatus, { label: string; tone: BadgeTone }> = {
  [CAPTAIN_STATUS.Active]: { label: 'Account active', tone: 'active' },
  [CAPTAIN_STATUS.Suspended]: { label: 'Suspended', tone: 'upcoming' },
  [CAPTAIN_STATUS.Inactive]: { label: 'Inactive', tone: 'muted' },
}

const toDraft = (c?: Captain): CaptainDraft => ({
  fullName: c?.fullName ?? '',
  email: c?.email ?? '',
  phone: c?.phone ?? '',
  licenseNumber: c?.licenseNumber ?? '',
  licenseExpiresOn: c?.licenseExpiresOn ?? '',
})

type FormErrors = Partial<Record<keyof CaptainDraft | 'reason', string>>

function validate(draft: CaptainDraft, reason: string | null): FormErrors {
  const errors: FormErrors = {}
  if (!draft.fullName.trim()) errors.fullName = t('Name is required.')
  if (!isEmail(draft.email)) errors.email = t('Enter a valid email address.')
  if (!isPhone(draft.phone)) errors.phone = t('Enter a valid phone number.')
  if (!draft.licenseNumber.trim()) errors.licenseNumber = t('License number is required.')
  if (!draft.licenseExpiresOn) errors.licenseExpiresOn = t('License expiry date is required.')
  if (reason !== null && !reason.trim()) errors.reason = t('Enter a reason.')
  return errors
}

function CaptainForm({ captain, onDone, onCancel }: { captain?: Captain; onDone: () => void; onCancel: () => void }) {
  const [draft, setDraft] = useState(() => toDraft(captain))
  const [reason, setReason] = useState('')
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  // Sửa hồ sơ cần lý do (BR-CAP-07); tạo mới thì không.
  const reasonNeeded = captain ? reason : null
  const errors = showErrors ? validate(draft, reasonNeeded) : {}
  const set = (key: keyof CaptainDraft) => (e: { target: { value: string } }) =>
    setDraft((d) => ({ ...d, [key]: e.target.value }))
  const emailHint = captain
    ? 'Changing the email signs the Captain out and sends a new activation link.'
    : 'An activation link is sent to this address.'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.keys(validate(draft, reasonNeeded)).length > 0) {
      setShowErrors(true)
      return
    }
    const body = { ...draft, fullName: draft.fullName.trim(), email: draft.email.trim(), phone: draft.phone.trim() }
    const saved = await run(async () => {
      if (captain) await updateCaptain(captain.id, body, reason)
      else await createCaptain(body)
      return true
    })
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <TextField label="Full name" required value={draft.fullName} onChange={set('fullName')} error={errors.fullName} maxLength={200} />
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Email" type="email" required value={draft.email} onChange={set('email')} error={errors.email}
          maxLength={256} hint={emailHint} />
        <TextField label="Phone" type="tel" required value={draft.phone} onChange={set('phone')} error={errors.phone} maxLength={30} />
        <TextField label="License number" required value={draft.licenseNumber} onChange={set('licenseNumber')}
          error={errors.licenseNumber} maxLength={100} />
        <TextField label="License expires on" type="date" required value={draft.licenseExpiresOn}
          onChange={set('licenseExpiresOn')} error={errors.licenseExpiresOn} />
      </div>
      {captain && (
        <TextField label="Reason" required value={reason} onChange={(e) => setReason(e.target.value)} error={errors.reason} maxLength={1000} />
      )}
      <FormActions error={error} busy={busy} submitLabel={captain ? 'Save captain' : 'Create captain'} onCancel={onCancel} />
    </form>
  )
}

function StatusDialog({ captain, onDone, onClose }: { captain: Captain; onDone: () => void; onClose: () => void }) {
  const options = ([CAPTAIN_STATUS.Active, CAPTAIN_STATUS.Suspended, CAPTAIN_STATUS.Inactive] as CaptainStatus[]).filter(
    (status) => status !== captain.status,
  )
  const [status, setStatus] = useState<CaptainStatus>(options[0])
  // BE từ chối bật Captain chưa kích hoạt (BR-CAP-02); báo sớm ngay trên form.
  const activationBlocked = status === CAPTAIN_STATUS.Active && (captain.invitationPending || !captain.emailConfirmed)
  return (
    <ReasonConfirmModal
      title="Change captain status"
      message={t('Change the status of {name}? Their current sessions are signed out.', { name: captain.fullName })}
      confirmLabel="Change status"
      action={(reason) => changeCaptainStatus(captain.id, status, reason)}
      onDone={onDone}
      onClose={onClose}
    >
      <SelectField
        label="New status"
        value={status}
        onChange={(e) => setStatus(Number(e.target.value) as CaptainStatus)}
        error={activationBlocked ? t('The Captain must accept the activation invitation first.') : undefined}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {t(STATUS_LABEL[option].label)}
          </option>
        ))}
      </SelectField>
    </ReasonConfirmModal>
  )
}

function CaptainList({ boats, captainNames, onChanged }: { boats: AdminBoat[]; captainNames: Map<string, string>; onChanged: () => void }) {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<CaptainStatus | ''>('')
  const [dialog, setDialog] = useState<Dialog>(null)
  const captains = useFetch(() => listCaptains(page, search, status), [page, search, status])

  if (captains.error) return <ErrorState onRetry={captains.retry} />
  if (captains.loading || !captains.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    captains.retry()
    onChanged()
  }

  const columns: Column<Captain>[] = [
    { header: 'Name', render: (c) => <span className="font-semibold">{c.fullName}</span> },
    {
      header: 'Contact',
      render: (c) => (
        <span>
          {c.email}
          <br />
          <span className="text-on-surface-variant">{c.phone ?? '—'}</span>
        </span>
      ),
    },
    {
      header: 'License',
      render: (c) => (
        <span>
          {c.licenseNumber ?? '—'}
          <br />
          <span className="text-on-surface-variant">{c.licenseExpiresOn ?? '—'}</span>
        </span>
      ),
    },
    {
      header: 'Status',
      render: (c) =>
        c.invitationPending ? (
          <StatusBadge tone="upcoming" label="Awaiting activation" />
        ) : (
          <StatusBadge tone={STATUS_LABEL[c.status].tone} label={STATUS_LABEL[c.status].label} />
        ),
    },
    {
      header: 'Boat',
      render: (c) => {
        const boat = boats.find((b) => b.id === c.boatId)
        return boat ? (
          <span className="font-mono font-semibold">{boat.code}</span>
        ) : (
          <span className="text-on-surface-variant">{t('Not assigned')}</span>
        )
      },
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (c) => (
        <span className="inline-flex flex-wrap justify-end gap-1">
          <Button size="sm" variant="neutral" icon="edit" onClick={() => setDialog({ kind: 'form', captain: c })}>
            {t('Edit')}
          </Button>
          {(c.invitationPending || !c.emailConfirmed) && (
            <Button size="sm" variant="neutral" icon="mail" onClick={() => setDialog({ kind: 'invite', captain: c })}>
              {t('Resend invitation')}
            </Button>
          )}
          <Button size="sm" variant="neutral" icon="sailing" onClick={() => setDialog({ kind: 'boat', captain: c })}>
            {c.boatId ? t('Change boat') : t('Assign boat')}
          </Button>
          <Button size="sm" variant="neutral" icon="swap_horiz" onClick={() => setDialog({ kind: 'status', captain: c })}>
            {t('Change status')}
          </Button>
        </span>
      ),
    },
  ]

  return (
    <>
      <div className="mb-3 flex justify-end">
        <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
          {t('Add captain')}
        </Button>
      </div>
      <FilterBar>
        <TextField
          label="Search"
          placeholder={t('Search by name or email')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
        />
        <FilterChips
          label="Status"
          value={String(status)}
          onChange={(value) => {
            setStatus(value === '' ? '' : (Number(value) as CaptainStatus))
            setPage(1)
          }}
          options={[
            { value: '', label: 'All statuses' },
            ...Object.values(CAPTAIN_STATUS).map((value) => ({ value: String(value), label: STATUS_LABEL[value].label })),
          ]}
        />
      </FilterBar>
      <DataTable columns={columns} rows={captains.data.items} rowKey={(c) => c.id} empty="No captains match this filter." />
      <div className="mt-3">
        <Pagination pageNumber={captains.data.pageNumber} totalPages={captains.data.totalPages} onChange={setPage} />
      </div>

      {dialog?.kind === 'form' && (
        <Modal title={dialog.captain ? 'Edit captain' : 'Add captain'} onClose={() => setDialog(null)}>
          <CaptainForm captain={dialog.captain} onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}
      {dialog?.kind === 'status' && (
        <StatusDialog captain={dialog.captain} onDone={closeAndReload} onClose={() => setDialog(null)} />
      )}
      {dialog?.kind === 'boat' && (
        <AssignCaptainDialog captain={dialog.captain} boats={boats} captainNames={captainNames}
          onDone={closeAndReload} onClose={() => setDialog(null)} />
      )}
      {dialog?.kind === 'invite' && (
        <ReasonConfirmModal
          title="Resend invitation"
          message={t('Send a new activation link to {email}? The previous link stops working.', { email: dialog.captain.email })}
          confirmLabel="Send invitation"
          action={(reason) => resendCaptainInvitation(dialog.captain.id, reason)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}

type Tab = 'captains' | 'alerts' | 'audit'

export default function CaptainsPage() {
  useDocumentTitle('Captains')
  const [tab, setTab] = useState<Tab>('captains')
  const [version, setVersion] = useState(0)
  const people = useFetch(listAllCaptains, [version])
  const boats = useFetch(() => getBoats(false), [version])

  if (people.error || boats.error) return <ErrorState onRetry={() => { people.retry(); boats.retry() }} />
  if (people.loading || boats.loading || !people.data || !boats.data) return <PageLoader />

  const captainNames = new Map(people.data.map((c) => [c.id, c.fullName]))
  const boatCodes = new Map(boats.data.map((b) => [b.id, b.code]))
  return (
    <>
      <PageHeader
        title="Captains"
        description="Create Captain accounts, send activation invitations, assign them to boats and control who can sign in."
      />
      <Tabs
        label="Captain management"
        value={tab}
        onChange={setTab}
        tabs={[
          { key: 'captains', label: 'Captains' },
          { key: 'alerts', label: 'Alerts' },
          { key: 'audit', label: 'Activity log' },
        ]}
      >
        {tab === 'captains' && (
          <CaptainList boats={boats.data} captainNames={captainNames} onChanged={() => setVersion((v) => v + 1)} />
        )}
        {tab === 'alerts' && <CaptainAlertsPanel captainNames={captainNames} boatCodes={boatCodes} />}
        {tab === 'audit' && <CaptainAuditPanel captainNames={captainNames} />}
      </Tabs>
    </>
  )
}
