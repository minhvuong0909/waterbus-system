import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import SelectField from './SelectField'
import TextField from './TextField'

describe('required fields', () => {
  it('marks the label and the control of a required text field', () => {
    render(<TextField label="Code" required />)
    const input = screen.getByLabelText('Code')
    expect((input as HTMLInputElement).required).toBe(true)
    expect(screen.getByText('Code').className).toContain("after:content-['*']")
  })

  it('leaves an optional field unmarked', () => {
    render(<TextField label="Notes" />)
    expect((screen.getByLabelText('Notes') as HTMLInputElement).required).toBe(false)
    expect(screen.getByText('Notes').className).not.toContain('after:content')
  })

  it('marks a required select', () => {
    render(
      <SelectField label="Route" required>
        <option value="a">A</option>
      </SelectField>,
    )
    expect((screen.getByLabelText('Route') as HTMLSelectElement).required).toBe(true)
  })
})
