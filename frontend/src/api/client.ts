import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { AuthResponse } from './types'

const ACCESS = 'erpos.access'
const REFRESH = 'erpos.refresh'

// Wrapped because storage can throw (private mode, blocked site data).
const store = {
  get: (k: string) => { try { return localStorage.getItem(k) } catch { return null } },
  set: (k: string, v: string) => { try { localStorage.setItem(k, v) } catch { /* ignore */ } },
  del: (k: string) => { try { localStorage.removeItem(k) } catch { /* ignore */ } },
}

export const tokens = {
  access: () => store.get(ACCESS),
  refresh: () => store.get(REFRESH),
  save: (r: AuthResponse) => { store.set(ACCESS, r.accessToken); store.set(REFRESH, r.refreshToken) },
  clear: () => { store.del(ACCESS); store.del(REFRESH) },
}

export const api = axios.create({ baseURL: '/api' })

api.interceptors.request.use(cfg => {
  const t = tokens.access()
  if (t) cfg.headers.Authorization = `Bearer ${t}`
  return cfg
})

let refreshing: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const rt = tokens.refresh()
  if (!rt) return null
  try {
    const { data } = await axios.post<AuthResponse>('/api/auth/refresh', { refreshToken: rt })
    tokens.save(data)
    return data.accessToken
  } catch {
    tokens.clear()
    return null
  }
}

api.interceptors.response.use(undefined, async (error: AxiosError) => {
  const original = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined
  if (error.response?.status === 401 && original && !original._retry && !original.url?.startsWith('/auth/')) {
    original._retry = true
    refreshing ??= refreshAccessToken().finally(() => { refreshing = null })
    const newToken = await refreshing
    if (newToken) {
      original.headers.Authorization = `Bearer ${newToken}`
      return api(original)
    }
    window.dispatchEvent(new Event('erpos:logout'))
  }
  return Promise.reject(error)
})

/** Human-readable message from an API error (ProblemDetails title). */
export function errorMessage(e: unknown): string {
  if (axios.isAxiosError(e)) {
    const d = e.response?.data as { title?: string; errors?: Record<string, string[]> } | undefined
    if (d?.errors) return Object.values(d.errors).flat().join(' ')
    if (d?.title) return d.title
    return e.message
  }
  return e instanceof Error ? e.message : 'Something went wrong'
}
