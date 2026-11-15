/** Response of `GET /api/v1/me/profile`. */
export interface Profile {
  id: string
  email: string
  fullName: string
  phoneNumber: string | null
  emailConfirmed: boolean
}
