export interface FaqItem {
  question: string
  answer: string
}

// English source text: the answers match what the booking flow actually does.
export const FAQ: FaqItem[] = [
  {
    question: 'How do I book a ticket?',
    answer:
      'Pick your departure and arrival piers and a date, choose a trip and a seat, enter the passenger details and pay. Your QR e-ticket appears in My Tickets as soon as the payment is confirmed.',
  },
  {
    question: 'What is the difference between the cabins?',
    answer:
      'The boat has a front cabin, a standard cabin and an outdoor deck. Each seat class has its own fare, shown on the seat map, and the fare is the same whichever piers you travel between.',
  },
  {
    question: 'Can I book several seats at once?',
    answer:
      'Yes, up to 10 seats in one booking. You enter the name of the passenger for each seat (email and phone are optional), plus the buyer’s name and email. Each ticket is issued in its passenger’s name; one booking has one QR code, which staff scan to check in the passengers who are present.',
  },
  {
    question: 'Which payment methods are accepted?',
    answer: 'Payment is made online through VNPay (QR code, bank card or e-wallet).',
  },
  {
    question: 'Can I get a refund?',
    answer:
      'Refunds happen when a trip is cancelled, or ends early before you reach your stop: 100% of what you paid for the affected tickets goes back to your original payment method. Tickets of passengers who miss check-in are not refunded.',
  },
  {
    question: 'When does boarding open?',
    answer:
      'Each pier has its own check-in window, shown on the trip page. Come to the waiting area of your boarding pier within that window and show your QR code to the staff, who check in each passenger. Passengers who miss the window cannot board and are not refunded.',
  },
]
