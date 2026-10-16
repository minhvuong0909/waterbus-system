/** Backend routes that the app calls (controllers use the `api/v1/[controller]` prefix). */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/v1/auth/login',
  },
  stations: '/api/v1/stations',
  checkouts: {
    create: '/api/v1/checkouts',
    byId: (orderId: string) => `/api/v1/checkouts/${orderId}`,
  },
  payments: {
    createVnPayUrl: '/api/v1/payments/create-vnpay-url',
    vnPayReturn: '/api/v1/payments/vnpay-return',
    reconcile: (orderId: string, paymentId: string) =>
      `/api/v1/payments/${orderId}/attempts/${paymentId}/reconcile`,
  },
  trips: {
    search: '/api/v1/trips/search',
  },
} as const
