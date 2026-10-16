import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import ErrorState from './ErrorState'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  failed: boolean
}

/** Catches a render error anywhere below it, so one broken page does not blank the whole app. */
export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { failed: false }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    if (import.meta.env.DEV) console.error(error, info.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children
    // A full reload clears whatever state made the render fail.
    return <ErrorState onRetry={() => window.location.reload()} />
  }
}
