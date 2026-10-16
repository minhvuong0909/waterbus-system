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
    question: 'What is the difference between the cabins?',
    answer:
      'The boat has a front cabin, a standard cabin and an outdoor deck. Front cabin and outdoor deck seats cost a little more, as shown on the seat map. Standard seats have no extra fee.',
  },
  {
    question: 'Can I book several seats at once?',
    answer:
      'Yes, up to 10 seats in one booking. You enter one contact, and every ticket of the booking is issued in that name.',
  },
  {
    question: 'Which payment methods are accepted?',
    answer: 'Payment is made online through VNPay (QR code, bank card or e-wallet).',
  },
  {
    question: 'Can I get a refund?',
    answer:
      'Refunds happen when a trip is cancelled, or ends early before you reach your stop. The amount you paid for the affected tickets is refunded to your original payment method, and its status shows on your booking page. Tickets of passengers who miss check-in are not refunded.',
  },
  {
    question: 'When does boarding open?',
    answer:
      'Boarding opens 10 minutes before departure. Arrive at the pier with your QR ticket and scan it at the turnstile.',
  },
]
