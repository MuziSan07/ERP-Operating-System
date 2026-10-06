import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { App as AntApp, ConfigProvider, theme } from 'antd'
import { AuthProvider } from './auth/AuthContext'
import App from './App'
import ErrorBoundary from './components/ErrorBoundary'
import { QueryErrorToaster, reportQueryError, shouldRetry } from './api/queryErrors'
import './index.css'

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: reportQueryError }),
  defaultOptions: { queries: { retry: shouldRetry, refetchOnWindowFocus: false, staleTime: 30_000 } },
})

const dark = window.matchMedia?.('(prefers-color-scheme: dark)').matches

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider
      theme={{
        algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm,
        token: { colorPrimary: '#2f54eb', borderRadius: 8 },
      }}
    >
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <QueryErrorToaster />
          <BrowserRouter>
            <AuthProvider>
              <ErrorBoundary>
                <App />
              </ErrorBoundary>
            </AuthProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  </StrictMode>,
)
