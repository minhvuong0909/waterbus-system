import type { Ewallet, NextStep, PaymentMethod } from '../features/payment/types'

export const PAYMENT_METHODS: PaymentMethod[] = [
  {
    id: 'qr',
    icon: 'qr_code_scanner',
    title: 'QR Payment',
    description: 'Instant bank or e-wallet scan',
  },
  { id: 'card', icon: 'credit_card', title: 'Bank Card', description: 'Credit or debit card' },
  {
    id: 'wallet',
    icon: 'account_balance_wallet',
    title: 'E-Wallet',
    description: 'Digital mobile wallet',
  },
]

export const EWALLETS: Ewallet[] = [
  { id: 'momo', name: 'MoMo', note: 'Pay with your MoMo balance' },
  { id: 'vnpay', name: 'VNPay', note: 'Pay via VNPay QR or app' },
  { id: 'zalopay', name: 'ZaloPay', note: 'Pay with your ZaloPay balance' },
]

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
