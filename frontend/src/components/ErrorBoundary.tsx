import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button, Result } from 'antd'

/** Catches rendering crashes so one broken screen shows a message instead of blanking the whole app. */
export default class ErrorBoundary extends Component<{ children: ReactNode }, { error?: Error }> {
  state: { error?: Error } = {}

  static getDerivedStateFromError(error: Error) {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Screen crashed', error, info.componentStack)
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <Result status="error" title="Something went wrong on this screen"
        subTitle="The rest of ERPOS still works. Try again, or open another page from the menu."
        extra={<Button type="primary" onClick={() => this.setState({ error: undefined })}>Try again</Button>} />
    )
  }
}
