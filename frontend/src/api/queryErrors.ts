import { useEffect } from 'react'
import { App } from 'antd'
import type { AxiosError } from 'axios'
import { errorMessage } from './client'

// Bridges the QueryClient (created outside React) to antd's message API (which needs the App context).
let show: ((error: unknown) => void) | undefined

const status = (e: unknown) => (e as AxiosError)?.response?.status

/** Don't retry what can't succeed: not signed in, no permission, not found, or a validation error. */
export const shouldRetry = (failures: number, error: unknown) => failures < 1 && ![400, 401, 403, 404, 409].includes(status(error) ?? 0)

/** Called by the query cache for every failed query. Server faults and lost connections are shown; expected answers
 * (403 no access, 404) are left to the page, which knows how to present them. */
export function reportQueryError(error: unknown) {
  const s = status(error)
  if (s === undefined || s >= 500) show?.(error)
}

/** Mount once inside antd's <App>. */
export function QueryErrorToaster() {
  const { message } = App.useApp()
  useEffect(() => {
    show = e => {
      const text = status(e) === undefined ? 'Cannot reach the server. Check your connection and try again.' : errorMessage(e)
      void message.error({ content: text, key: text }) // same text → one message, not a stack
    }
    return () => { show = undefined }
  }, [message])
  return null
}
