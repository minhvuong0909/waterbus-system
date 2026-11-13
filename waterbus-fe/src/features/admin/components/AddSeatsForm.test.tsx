import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import { addBoatSeats } from '../services/boatSeatService'
import type { SeatClass } from '../types'
import AddSeatsForm from './AddSeatsForm'

vi.mock('../services/boatSeatService')

const seatClasses: SeatClass[] = [
  { id: 'front', code: 'SC01', name: 'Khoang trước VIP', description: null, isActive: true, seatCount: 0 },
  { id: 'std', code: 'SC02', name: 'Tiêu chuẩn', description: null, isActive: true, seatCount: 0 },
  { id: 'deck', code: 'SC03', name: 'Boong ngoài', description: null, isActive: true, seatCount: 0 },
]

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  setActiveLocale('en')
})

const renderForm = (room = 60, existingSeats: { seatCode: string; rowNumber: number }[] = [], onDone = vi.fn()) =>
  render(<AddSeatsForm boatId="b1" seatClasses={seatClasses} existingSeats={existingSeats} room={room} onDone={onDone} onCancel={() => {}} />)

const fields = (label: string) => screen.getAllByLabelText(label) as HTMLInputElement[]

describe('AddSeatsForm', () => {
  it('fills the whole boat with one class by typing only the prefix', async () => {
    vi.mocked(addBoatSeats).mockResolvedValue({ addedCount: 1 } as never)
    const onDone = vi.fn()
    renderForm(60, [], onDone)

    expect(fields('Number of seats')[0].value).toBe('60')                  // mặc định: toàn bộ chỗ còn lại
    fireEvent.change(fields('Code prefix')[0], { target: { value: 's' } })
    expect(screen.getByText('Adds 60 seats (S01–S60).')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))

    await waitFor(() => expect(addBoatSeats).toHaveBeenCalledTimes(1))
    const [boatId, seats] = vi.mocked(addBoatSeats).mock.calls[0]
    expect(boatId).toBe('b1')
    expect(seats).toHaveLength(60)
    expect(seats[0]).toEqual({ seatCode: 'S01', seatClassId: 'front', rowNumber: 1, columnNumber: 1 })
    expect(seats[59]).toMatchObject({ seatCode: 'S60', rowNumber: 15, columnNumber: 4 })
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('adds several cabins in ONE request, rows continuing below the previous cabin', async () => {
    vi.mocked(addBoatSeats).mockResolvedValue({ addedCount: 1 } as never)
    renderForm(60)

    fireEvent.change(fields('Code prefix')[0], { target: { value: 'F' } })
    fireEvent.change(fields('Number of seats')[0], { target: { value: '12' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add another class' }))
    expect(fields('Number of seats')[1].value).toBe('48')                  // phần còn lại của tàu
    fireEvent.change(fields('Code prefix')[1], { target: { value: 'S' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add another class' }))
    fireEvent.change(fields('Code prefix')[2], { target: { value: 'O' } })
    fireEvent.change(fields('Number of seats')[1], { target: { value: '36' } })
    fireEvent.change(fields('Number of seats')[2], { target: { value: '12' } })

    expect(screen.getByText('Adds 60 seats (F01–F12, S01–S36, O01–O12).')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))

    await waitFor(() => expect(addBoatSeats).toHaveBeenCalledTimes(1))
    const seats = vi.mocked(addBoatSeats).mock.calls[0][1]
    expect(seats.filter((s) => s.seatClassId === 'front')).toHaveLength(12)
    expect(seats.filter((s) => s.seatClassId === 'std')).toHaveLength(36)
    expect(seats.filter((s) => s.seatClassId === 'deck')).toHaveLength(12)
    expect(seats.find((s) => s.seatCode === 'S01')).toMatchObject({ rowNumber: 4, columnNumber: 1 })
  })

  it('continues the numbering of a boat that already has seats', async () => {
    vi.mocked(addBoatSeats).mockResolvedValue({ addedCount: 1 } as never)
    renderForm(10, [{ seatCode: 'S01', rowNumber: 1 }, { seatCode: 'S08', rowNumber: 2 }])

    fireEvent.change(fields('Code prefix')[0], { target: { value: 'S' } })
    fireEvent.change(fields('Number of seats')[0], { target: { value: '2' } })

    expect(screen.getByText('Adds 2 seats (S09–S10).')).toBeTruthy()
  })

  it('refuses more seats than the boat has room for, and bad prefixes, without calling the server', async () => {
    renderForm(10)

    fireEvent.change(fields('Code prefix')[0], { target: { value: 'S1' } })     // chữ số không hợp lệ
    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))
    expect(await screen.findByText('Use 1 to 6 letters.')).toBeTruthy()

    fireEvent.change(fields('Code prefix')[0], { target: { value: 'S' } })
    fireEvent.change(fields('Number of seats')[0], { target: { value: '11' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))
    expect(await screen.findByText('Enter a number from 1 to 10.')).toBeTruthy()

    expect(addBoatSeats).not.toHaveBeenCalled()
  })

  it('blocks a total above the room even when each line alone fits', () => {
    renderForm(10)

    fireEvent.change(fields('Code prefix')[0], { target: { value: 'F' } })
    fireEvent.change(fields('Number of seats')[0], { target: { value: '6' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add another class' }))
    fireEvent.change(fields('Code prefix')[1], { target: { value: 'S' } })
    fireEvent.change(fields('Number of seats')[1], { target: { value: '6' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))

    expect(screen.getByText('You can add at most 10 seats now.')).toBeTruthy()
    expect(addBoatSeats).not.toHaveBeenCalled()
  })

  it('removes a line and shows the server message when the batch is refused', async () => {
    vi.mocked(addBoatSeats).mockRejectedValue(new Error('down'))
    renderForm(60)
    fireEvent.change(fields('Code prefix')[0], { target: { value: 'S' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add another class' }))
    expect(fields('Code prefix')).toHaveLength(2)
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove' })[0])
    expect(fields('Code prefix')).toHaveLength(1)

    fireEvent.click(screen.getByRole('button', { name: 'Add seats' }))

    expect(await screen.findByRole('alert')).toBeTruthy()
  })
})
