import { useState } from 'react'
import type { FormEvent } from 'react'
import {
  Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, PageHeader, PageLoader, Pagination, TextField,
} from '../../components/ui'
import type { Column } from '../../components/ui'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import FormActions from '../../features/admin/components/FormActions'
import ReasonConfirmModal from '../../features/admin/components/ReasonConfirmModal'
import StatusBadge, { ActiveBadge } from '../../features/admin/components/StatusBadge'
import {
  changeStaffAccountStatus, createStaffAccount, listStaffAccounts, resendStaffInvitation, updateStaffAccount,
} from '../../features/admin/services/personnelService'
import type { StaffAccount, StaffAccountDraft } from '../../features/admin/types/personnel'
import { isEmail, isPhone } from '../../features/admin/utils/personnel'
import { useDocumentTitle, useFetch, useSubmit } from '../../hooks'
import { t } from '../../i18n'

type Dialog =
  | { kind: 'form'; staff?: StaffAccount }
  | { kind: 'status'; staff: StaffAccount }
  | { kind: 'invite'; staff: StaffAccount }
  | null

const toDraft = (s?: StaffAccount): StaffAccountDraft => ({
  fullName: s?.fullName ?? '',
  email: s?.email ?? '',
  phone: s?.phone ?? '',
  staffCode: s?.staffCode ?? '',
})

type FormErrors = Partial<Record<keyof StaffAccountDraft, string>>

function validate(draft: StaffAccountDraft): FormErrors {
  const errors: FormErrors = {}
  if (!draft.fullName.trim()) errors.fullName = t('Name is required.')
  if (!isEmail(draft.email)) errors.email = t('Enter a valid email address.')
  if (!isPhone(draft.phone)) errors.phone = t('Enter a valid phone number.')
  return errors
}

function StaffForm({ staff, onDone, onCancel }: { staff?: StaffAccount; onDone: () => void; onCancel: () => void }) {
  const [draft, setDraft] = useState(() => toDraft(staff))
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const errors = showErrors ? validate(draft) : {}
  const set = (key: keyof StaffAccountDraft) => (e: { target: { value: string } }) =>
    setDraft((d) => ({ ...d, [key]: e.target.value }))
  const emailHint = staff
    ? 'Changing the email turns login off until the new address is activated.'
    : 'An activation link is sent to this address.'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.keys(validate(draft)).length > 0) {
      setShowErrors(true)
      return
    }
    const body = { ...draft, fullName: draft.fullName.trim(), email: draft.email.trim(), phone: draft.phone.trim() }
    const saved = await run(async () => {
      if (staff) await updateStaffAccount(staff.id, body)
      else await createStaffAccount(body)
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
        <TextField label="Staff code" value={draft.staffCode} onChange={set('staffCode')} error={errors.staffCode} maxLength={50}
          hint="Optional. Must be unique." />
      </div>
      <FormActions error={error} busy={busy} submitLabel={staff ? 'Save staff' : 'Create staff'} onCancel={onCancel} />
    </form>
  )
}

export default function StaffAccountsPage() {
  useDocumentTitle('Staff accounts')
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [active, setActive] = useState<'' | 'true' | 'false'>('')
  const [dialog, setDialog] = useState<Dialog>(null)
  const staff = useFetch(() => listStaffAccounts(page, search, active === '' ? '' : active === 'true'), [page, search, active])

  if (staff.error) return <ErrorState onRetry={staff.retry} />
  if (staff.loading || !staff.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    staff.retry()
  }

  const columns: Column<StaffAccount>[] = [
    { header: 'Name', render: (s) => <span className="font-semibold">{s.fullName}</span> },
    { header: 'Staff code', render: (s) => <span className="font-mono">{s.staffCode ?? '—'}</span> },
    {
      header: 'Contact',
      render: (s) => (
        <span>
          {s.email}
          <br />
          <span className="text-on-surface-variant">{s.phone ?? '—'}</span>
        </span>
      ),
    },
    {
      header: 'Status',
      render: (s) =>
        s.invitationPending ? <StatusBadge tone="upcoming" label="Awaiting activation" /> : <ActiveBadge active={s.isActive} />,
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (s) => (
        <span className="inline-flex flex-wrap justify-end gap-1">
          <Button size="sm" variant="neutral" icon="edit" onClick={() => setDialog({ kind: 'form', staff: s })}>
            {t('Edit')}
          </Button>
          {(s.invitationPending || !s.emailConfirmed) && (
            <Button size="sm" variant="neutral" icon="mail" onClick={() => setDialog({ kind: 'invite', staff: s })}>
              {t('Resend invitation')}
            </Button>
          )}
          {!s.invitationPending && (
            <Button size="sm" variant="neutral" icon={s.isActive ? 'block' : 'check_circle'} onClick={() => setDialog({ kind: 'status', staff: s })}>
              {s.isActive ? t('Turn off login') : t('Turn on login')}
            </Button>
          )}
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Staff accounts"
        description="Create station Staff accounts, send activation invitations and control who can sign in to the scanner."
        actions={
          <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add staff')}
          </Button>
        }
      />
      <FilterBar>
        <TextField
          label="Search"
          placeholder={t('Search by name, email or code')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
        />
        <FilterChips
          label="Status"
          value={active}
          onChange={(value) => {
            setActive(value as '' | 'true' | 'false')
            setPage(1)
          }}
          options={[
            { value: '', label: 'All statuses' },
            { value: 'true', label: 'Active only' },
            { value: 'false', label: 'Inactive only' },
          ]}
        />
      </FilterBar>
      <DataTable columns={columns} rows={staff.data.items} rowKey={(s) => s.id} empty="No staff accounts match this filter." />
      <div className="mt-3">
        <Pagination pageNumber={staff.data.pageNumber} totalPages={staff.data.totalPages} onChange={setPage} />
      </div>

      {dialog?.kind === 'form' && (
        <Modal title={dialog.staff ? 'Edit staff' : 'Add staff'} onClose={() => setDialog(null)}>
          <StaffForm staff={dialog.staff} onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}
      {dialog?.kind === 'status' && (
        <ConfirmModal
          title={dialog.staff.isActive ? 'Turn off login' : 'Turn on login'}
          message={
            dialog.staff.isActive
              ? t('Turn off login for {name}? They can no longer sign in to the scanner.', { name: dialog.staff.fullName })
              : t('Turn on login for {name}?', { name: dialog.staff.fullName })
          }
          confirmLabel={dialog.staff.isActive ? 'Turn off login' : 'Turn on login'}
          tone={dialog.staff.isActive ? 'danger' : 'primary'}
          action={() => changeStaffAccountStatus(dialog.staff.id, !dialog.staff.isActive)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.kind === 'invite' && (
        <ReasonConfirmModal
          title="Resend invitation"
          message={t('Send a new activation link to {email}? The previous link stops working.', { email: dialog.staff.email })}
          confirmLabel="Send invitation"
          action={(reason) => resendStaffInvitation(dialog.staff.id, reason)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}
