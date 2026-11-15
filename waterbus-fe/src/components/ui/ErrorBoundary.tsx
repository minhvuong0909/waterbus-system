import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import ErrorState from './ErrorState'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  failed: boolean
}

/** Bắt lỗi render ở bất kỳ đâu bên dưới, để một trang hỏng không làm trắng cả app. */
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
    // Tải lại toàn trang để xoá trạng thái đã làm render lỗi.
    return <ErrorState onRetry={() => window.location.reload()} />
  }
}
