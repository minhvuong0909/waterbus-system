import { describe, expect, it } from 'vitest'
import { attentionItems, isWithoutBoat } from './attention'

describe('attentionItems', () => {
  it('lists the urgent work first, with counts and links', () => {
    const items = attentionItems({
      trips: [
        { status: 'Scheduled', boatId: '' },
        { status: 'Scheduled', boatId: 'b1' },
        { status: 'Cancelled', boatId: '' },
        { status: 'Suspended', boatId: 'b1' },
      ],
      alerts: [{ pendingDecisionCount: 3 }, { pendingDecisionCount: 2 }],
      pendingRefunds: 4,
      reconcile: 1,
    })
    expect(items.map((x) => [x.key, x.count, x.to])).toEqual([
      ['decisions', 5, '/admin/incidents'],
      ['suspended', 1, '/admin/trips?filter=Suspended'],
      ['noBoat', 1, '/admin/trips?filter=no-boat'],
      ['reconcile', 1, '/admin/refunds?tab=reconcile'],
      ['pendingRefunds', 4, '/admin/refunds?tab=pending'],
    ])
  })

  it('leaves out work with nothing to do, and refund counts it could not read', () => {
    expect(attentionItems({ trips: [], alerts: [{ pendingDecisionCount: 0 }] })).toEqual([])
  })

  it('counts the decisions of loaded trips when an alert has them', () => {
    const [item] = attentionItems({
      trips: [],
      alerts: [{ pendingDecisionCount: 9, trips: [{ tripId: 't1', decision: null }, { tripId: 't2', decision: 'Cancel' }] }],
    })
    expect(item.count).toBe(1)
  })
})

describe('isWithoutBoat', () => {
  it('is true only for a scheduled trip that has no boat yet', () => {
    expect(isWithoutBoat({ status: 'Scheduled', boatId: '' })).toBe(true)
    expect(isWithoutBoat({ status: 'Boarding', boatId: '' })).toBe(false)
    expect(isWithoutBoat({ status: 'Scheduled', boatId: 'b1' })).toBe(false)
  })
})
