import type { AmenityDetail, BoardingStep } from '../features/trips/types'

// Nội dung biên tập của trang chi tiết chuyến, backend không có API cho phần này.
export const ONBOARD_AMENITIES: AmenityDetail[] = [
  {
    icon: 'ac_unit',
    title: 'Air-Conditioned Cabin',
    description: 'Comfortable indoor temperature with river views.',
  },
  {
    icon: 'chair',
    title: 'Comfortable Seating',
    description: 'Ergonomic transit seating with scenic river views.',
  },
  {
    icon: 'luggage',
    title: 'Luggage Space',
    description: 'Dedicated space for carry-on luggage and personal items.',
  },
  {
    icon: 'accessible_forward',
    title: 'Step-Free Access',
    description: 'Ramps and accessibility support for all passengers.',
  },
]

export const BOARDING_STEPS: BoardingStep[] = [
  { title: 'Arrive at Pier', description: 'Arrive at {origin} before departure time.' },
  {
    title: 'Present QR Ticket',
    description: 'Display your Smart Waterbus QR e-ticket on your mobile screen.',
  },
  {
    title: 'Verify & Board',
    description: 'Staff verifies your ticket at the pier gate prior to vessel boarding.',
  },
]
