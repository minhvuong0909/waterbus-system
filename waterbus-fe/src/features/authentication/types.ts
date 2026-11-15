export interface SignInPayload {
  email: string
  password: string
  remember: boolean
}



/** Response body of `POST /api/v1/auth/login` (AuthResponseDto in the backend). */
export interface LoginResponse {
  userId: string
  email: string
  fullName: string
  roles: string[]
  token: string
}

/** Fields of the sign-up form. The role is never sent: the backend always creates a customer (Passenger). */
export interface RegisterPayload {
  fullName: string
  email: string
  phoneNumber: string
}

/** The same answer for any email on sign-up and "send again". */
export interface AccountRequestAccepted {
  message: string
}
