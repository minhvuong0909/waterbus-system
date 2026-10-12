import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import { loadSession } from '../../services/authStorage'
import type { AuthSession, AuthUser } from './authenticationTypes'

export interface AuthenticationState {
  user: AuthUser | null
  token: string | null
}

// Restores the session saved by a previous visit (expired tokens are dropped by `loadSession`).
function initialState(): AuthenticationState {
  const session = loadSession()
  return { user: session?.user ?? null, token: session?.token ?? null }
}

const authenticationSlice = createSlice({
  name: 'authentication',
  initialState,
  reducers: {
    signedIn(state, action: PayloadAction<AuthSession>) {
      state.user = action.payload.user
      state.token = action.payload.token
    },
    signedOut(state) {
      state.user = null
      state.token = null
    },
  },
})

export const { signedIn, signedOut } = authenticationSlice.actions
export default authenticationSlice.reducer
