import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, tokens } from '../api/client'
import type { AuthResponse, Me } from '../api/types'

interface AuthState {
  me: Me | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  reload: () => Promise<void>
  /** Holds the permission at the given entity, or (without an entity) at any entity. */
  can: (permission: string, entityId?: string) => boolean
  isPlatformAdmin: boolean
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)
  const qc = useQueryClient()

  const reload = useCallback(async () => {
    if (!tokens.access()) { setMe(null); setLoading(false); return }
    try {
      const { data } = await api.get<Me>('/auth/me')
      setMe(data)
    } catch (e) {
      // Only a rejected session signs the user out; a network blip or server restart keeps the tokens for a retry.
      if ((e as { response?: { status?: number } }).response?.status === 401) tokens.clear()
      setMe(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void reload() }, [reload])

  const logout = useCallback(async () => {
    const rt = tokens.refresh()
    tokens.clear()
    setMe(null)
    qc.clear()
    if (rt) await api.post('/auth/logout', { refreshToken: rt }).catch(() => undefined)
  }, [qc])

  useEffect(() => {
    const onLogout = () => { void logout() }
    window.addEventListener('erpos:logout', onLogout)
    return () => window.removeEventListener('erpos:logout', onLogout)
  }, [logout])

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await api.post<AuthResponse>('/auth/login', { email, password })
    tokens.save(data)
    qc.clear()
    await reload()
  }, [qc, reload])

  const value = useMemo<AuthState>(() => {
    const byEntity = new Map((me?.entities ?? []).map(e => [e.id, new Set(e.permissions)]))
    return {
      me, loading, login, logout, reload,
      isPlatformAdmin: me?.user.userType === 'PlatformAdmin',
      can: (permission, entityId) =>
        entityId
          ? byEntity.get(entityId)?.has(permission) ?? false
          : [...byEntity.values()].some(s => s.has(permission)),
    }
  }, [me, loading, login, logout, reload])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

export const P = {
  entitiesView: 'core.entities.view',
  entitiesCreate: 'core.entities.create',
  entitiesEdit: 'core.entities.edit',
  entitiesDelete: 'core.entities.delete',
  modulesManage: 'core.modules.manage',
  usersView: 'core.users.view',
  usersCreate: 'core.users.create',
  usersEdit: 'core.users.edit',
  usersDelete: 'core.users.delete',
  usersAssign: 'core.users.assign',
  rolesView: 'core.roles.view',
  rolesManage: 'core.roles.manage',
  auditView: 'core.audit.view',
  employeesView: 'hr.employees.view',
  employeesCreate: 'hr.employees.create',
  employeesEdit: 'hr.employees.edit',
  employeesDelete: 'hr.employees.delete',
  departmentsView: 'hr.departments.view',
  departmentsCreate: 'hr.departments.create',
  departmentsEdit: 'hr.departments.edit',
  departmentsDelete: 'hr.departments.delete',
  attendanceView: 'hr.attendance.view',
  attendanceCreate: 'hr.attendance.create',
  leaveView: 'hr.leave.view',
  leaveCreate: 'hr.leave.create',
  leaveEdit: 'hr.leave.edit',
  leaveApprove: 'hr.leave.approve',
  hrSettings: 'hr.settings.manage',
  salaryView: 'payroll.salary_structures.view',
  salaryCreate: 'payroll.salary_structures.create',
  payrollView: 'payroll.payroll_runs.view',
  payrollCreate: 'payroll.payroll_runs.create',
  payrollApprove: 'payroll.payroll_runs.approve',
  payrollPost: 'payroll.payroll_runs.post',
  payslipsView: 'payroll.payslips.view',
  payrollSettings: 'payroll.settings.manage',
} as const
