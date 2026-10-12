export type PaymentMethodId = 'qr' | 'card' | 'wallet'

export interface PaymentMethod {
  id: PaymentMethodId
  icon: string
  title: string
  description: string
}

export interface Ewallet {
  id: string
  name: string
  note: string
}

export interface NextStep {
  number: string
  title: string
  description: string
  chipIcon: string
  chipLabel: string
}
