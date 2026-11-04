import type { HomeData } from '../features/home/types'

// Nội dung biên tập của trang chủ (backend không có API cho phần này). Địa danh lấy từ content/explore.
export const HOME_DATA: HomeData = {
  routeFilters: [
    { id: 'all', label: 'All Trips' },
    { id: 'transit', label: 'Regular' },
    { id: 'sightseeing', label: 'Sightseeing' },
  ],
  steps: [
    {
      number: '01',
      title: 'Plan Journey',
      description: 'Pick your departure station, destination pier, and preferred schedule.',
      numberClass: 'bg-surface text-deep-river border border-outline-variant/30',
    },
    {
      number: '02',
      title: 'Choose Seat',
      description:
        'Pick a seat in the front cabin, the standard cabin or on the open deck; each class has its own fare.',
      numberClass: 'bg-surface text-teal-flow border border-outline-variant/30',
    },
    {
      number: '03',
      title: 'Pay Securely',
      description:
        'Pay online through VNPay; your e-ticket is issued once the payment is confirmed.',
      numberClass: 'bg-surface text-sky-aqua border border-outline-variant/30',
    },
    {
      number: '04',
      title: 'Board with QR',
      description:
        'Show your QR code to the staff at your boarding pier within its check-in window, then board.',
      numberClass: 'bg-teal-flow text-on-primary',
    },
  ],
}
