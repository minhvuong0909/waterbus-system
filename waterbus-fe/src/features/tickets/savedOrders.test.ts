import { afterEach, describe, expect, it } from 'vitest'
import {
  forgetOrder,
  loadSavedOrders,
  manageLinkPath,
  manageLinkUrl,
  parseManageLink,
  saveOrder,
} from './savedOrders'

afterEach(() => window.localStorage.clear())

describe('saved orders', () => {
  it('keeps the newest order first without duplicates', () => {
    saveOrder('o1', 'tk1')
    saveOrder('o2', 'tk2')
    saveOrder('o1', 'tk1-new')
    expect(loadSavedOrders().map((x) => [x.orderId, x.accessToken])).toEqual([['o2', 'tk2'], ['o1', 'tk1-new']])
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

describe('manage link', () => {
  it('round-trips a full link and a bare path', () => {
    expect(manageLinkPath('o-1', 'a+b/c')).toBe('/manage/a%2Bb%2Fc?orderId=o-1')
    expect(parseManageLink(manageLinkUrl('o-1', 'a+b/c'))).toEqual({ orderId: 'o-1', accessToken: 'a+b/c' })
    expect(parseManageLink('  /manage/tok?orderId=o-2 ')).toEqual({ orderId: 'o-2', accessToken: 'tok' })
  })

  it('rejects text that is not a manage link', () => {
    expect(parseManageLink('')).toBeNull()
    expect(parseManageLink('WB123456')).toBeNull()
    expect(parseManageLink('/manage/tok')).toBeNull()
    expect(parseManageLink('/tickets?orderId=o1')).toBeNull()
  })
})
