export interface FaqItem {
  question: string
  answer: string
}

export interface HelpHighlight {
  icon: string
  title: string
  description: string
}

export const HELP_HIGHLIGHTS: HelpHighlight[] = [
  {
    icon: 'confirmation_number',
    title: 'Book online',
    description: 'Choose your trip and seat, pay securely and get a QR e-ticket instantly.',
  },
  {
    icon: 'radar',
    title: 'Track your vessel',
    description: 'See where your boat is and when it reaches your pier.',
  },
  {
    icon: 'energy_savings_leaf',
    title: 'Electric river transit',
    description: 'Quiet, low-emission crossings along Saigon’s river corridor.',
  },
]

// English source text: the answers match what the booking flow actually does.
export const FAQ: FaqItem[] = [
  {
    question: 'How do I book a ticket?',
    answer:
      'Pick your departure and arrival piers and a date, choose a trip and a seat, enter the passenger details and pay. Your QR e-ticket is ready straight away in My Tickets.',
  },
  {
    question: 'What is the difference between standard and VIP seats?',
    answer:
      'VIP seats are in the aft lounge (rows 5–6) with priority disembarkation, for a surcharge shown on the seat map. Standard seats have no extra fee.',
  },
  {
    question: 'Which payment methods are accepted?',
    answer: 'QR payment, bank cards, and the MoMo, VNPay and ZaloPay e-wallets.',
  },
  {
    question: 'Can I get a refund?',
    answer:
      'Refunds are available up to 30 minutes before departure. Open My Tickets, choose the ticket and select Request Refund.',
  },
  {
    question: 'When does boarding open?',
    answer:
      'Boarding opens 10 minutes before departure. Arrive at the pier with your QR ticket and scan it at the turnstile.',
  },
]
