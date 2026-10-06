export type UserType = 'PlatformAdmin' | 'SuperAdmin' | 'Admin' | 'Manager' | 'Employee'
export type TenantStatus = 'Active' | 'Suspended' | 'Trial'
export type IndustryType =
  | 'General' | 'Ngo' | 'Tourism' | 'Hotel' | 'Travel' | 'Logistics' | 'SoftwareServices' | 'Retail' | 'Manufacturing'

export const INDUSTRIES: { value: IndustryType; label: string }[] = [
  { value: 'General', label: 'General' },
  { value: 'Ngo', label: 'NGO / Non-profit' },
  { value: 'Tourism', label: 'Tourism' },
  { value: 'Hotel', label: 'Hotel & Hospitality' },
  { value: 'Travel', label: 'Travel Agency' },
  { value: 'Logistics', label: 'Logistics' },
  { value: 'SoftwareServices', label: 'Software Services' },
  { value: 'Retail', label: 'Retail' },
  { value: 'Manufacturing', label: 'Manufacturing' },
]

export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number }

export interface User {
  id: string; email: string; fullName: string; phone?: string; userType: UserType; isActive: boolean
  primaryEntityId?: string; primaryEntityName?: string; lastLoginAt?: string; createdAt: string
}

export interface Tenant {
  id: string; name: string; code: string; status: TenantStatus; contactEmail?: string; contactPhone?: string
  country?: string; maxUsers: number; createdAt: string; userCount: number; entityCount: number
}

export interface EntityAccess {
  id: string; parentId?: string; name: string; code: string; industry: IndustryType; depth: number
  isActive: boolean; modules: string[]; permissions: string[]
}

export interface Me { user: User; tenant?: Tenant; entities: EntityAccess[] }

export interface AuthResponse { accessToken: string; expiresAt: string; refreshToken: string; user: User }

export interface Entity {
  id: string; parentId?: string; name: string; code: string; industry: IndustryType; depth: number; isActive: boolean
  email?: string; phone?: string; address?: string; city?: string; country?: string; currency: string; timeZone: string
  taxNumber?: string; childCount: number; userCount: number; modules: string[]
}

export interface ModuleState {
  code: string; name: string; description: string; alwaysOn: boolean; enabled: boolean
  availableFromParent: boolean; suggested: boolean
}

export interface Role {
  id: string; name: string; description?: string; isSystem: boolean; permissions: string[]; assignmentCount: number
}

export interface PermissionDef { code: string; module: string; resource: string; action: string; description: string }
export interface PermissionGroup { module: string; moduleName: string; permissions: PermissionDef[] }

export interface Assignment {
  id: string; roleId: string; roleName: string; entityId: string; entityName: string; includeDescendants: boolean
}
export interface Override {
  id: string; permissionCode: string; entityId: string; entityName: string; isGranted: boolean; includeDescendants: boolean
}
export interface UserAccess { assignments: Assignment[]; overrides: Override[]; effective: Record<string, string[]> }

export interface AuditLog {
  id: number; userId?: string; userName?: string; action: string; tableName: string; recordId?: string
  changes?: string; timestamp: string
}

export interface Dashboard {
  entities: number; users: number; activeUsers: number; roles: number
  usersByType: Record<string, number>; entitiesByIndustry: Record<string, number>
}
