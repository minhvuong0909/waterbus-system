import { useState } from 'react'
import type { FormEvent } from 'react'
import { TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../hooks/useAdmin'
import { createSeatClass, updateSeatClass } from '../services/seatClassService'
import type { SeatClass } from '../types'
import FormActions from './FormActions'

/** Adds a seat class or renames one. The code is fixed once created: fares refer to the class. */
export default function SeatClassForm({
  seatClass,
  onDone,
  onCancel,
}: {
  seatClass?: SeatClass
  onDone: () => void
  onCancel: () => void
}) {
  const [code, setCode] = useState(seatClass?.code ?? '')
  const [name, setName] = useState(seatClass?.name ?? '')
  const [description, setDescription] = useState(seatClass?.description ?? '')
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()

  const codeError = code.trim() ? undefined : t('Code is required.')
  const nameError = name.trim() ? undefined : t('Name is required.')

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (codeError || nameError) {
      setShowErrors(true)
      return
    }
    const details = { name: name.trim(), description: description.trim() || null }
    const saved = await run(async () => {
      if (seatClass) await updateSeatClass(seatClass.id, details)
      else await createSeatClass({ code: code.trim().toUpperCase(), ...details })
      return true
    })
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField
          label="Code"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          error={showErrors ? codeError : undefined}
          disabled={Boolean(seatClass)}
          maxLength={20}
          placeholder="STANDARD"
        />
        <TextField
          label="Name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={showErrors ? nameError : undefined}
          maxLength={50}
        />
      </div>
      <TextField
        label="Description"
        hint="Optional. Shown to staff only."
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        maxLength={500}
      />
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
