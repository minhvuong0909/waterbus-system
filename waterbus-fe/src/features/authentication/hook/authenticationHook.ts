import { useCallback } from 'react'
import { clearSession, saveSession } from '../../../services/authStorage'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { signedIn, signedOut } from '../authenticationSlice'
import type { AuthSession } from '../authenticationTypes'

/** Current user plus the actions that start and end a session. */
export function useAuth() {
  const dispatch = useAppDispatch()
  const user = useAppSelector((s) => s.authentication.user)

  /** Keeps `session` for this tab (or across visits when `remember`) and marks the user signed in. */
  const startSession = useCallback(
    (session: AuthSession, remember = true) => {
      saveSession(session, remember)
      dispatch(signedIn(session))
    },
    [dispatch],
  )

  const signOut = useCallback(() => {
    clearSession()
    dispatch(signedOut())
  }, [dispatch])

  return { user, isAuthenticated: user !== null, startSession, signOut }
}
