import type { NextStep } from '../features/payment/types'

// {pier} is replaced with the origin pier name by the page.
export const NEXT_STEPS: NextStep[] = [
  {
    number: '01',
    title: 'Access your QR ticket',
    description:
      'Keep your digital ticket handy on your screen or access it anytime from your account.',
    chipIcon: 'qr_code_2',
    chipLabel: 'Digital Ticket Ready',
  },
  {
    number: '02',
    title: 'Arrive at {pier}',
    description: 'Make your way to {pier} before scheduled departure time.',
    chipIcon: 'location_on',
    chipLabel: '{pier}',
  },
  {
    number: '03',
    title: 'Present your ticket for boarding',
    description: 'Present your QR ticket at the boarding point to board your river crossing.',
    chipIcon: 'directions_boat',
    chipLabel: 'Board Crossing',
  },
]
