import { afterEach, describe, expect, it } from 'vitest'
import { forgetOrder, loadSavedOrders, saveOrder } from './orderStorage'

afterEach(() => window.localStorage.clear())

describe('saved orders', () => {
  it('keeps the newest order first without duplicates', () => {
    saveOrder('o1', 'tk1')
    saveOrder('o2', 'tk2')
    saveOrder('o1', 'tk1-new')
    expect(loadSavedOrders().map((x) => [x.orderId, x.accessToken])).toEqual([
      ['o2', 'tk2'],
      ['o1', 'tk1-new'],
    ])
  })

  it('forgets one order from the device', () => {
    saveOrder('o1', 'tk1')
    saveOrder('o2', 'tk2')
    forgetOrder('o1')
    expect(loadSavedOrders().map((x) => x.orderId)).toEqual(['o2'])
  })

  it('ignores corrupt storage', () => {
    window.localStorage.setItem('waterbus.orders', '{not json')
    expect(loadSavedOrders()).toEqual([])
    window.localStorage.setItem('waterbus.orders', JSON.stringify([{ orderId: 1 }]))
    expect(loadSavedOrders()).toEqual([])
  })
})
