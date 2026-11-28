import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { AdminRole } from './types'

/** What a platform admin may do (the server's Permissions codes). */
export type Perm = 'shops.approve' | 'shops.open' | 'shops.review' | 'support' | 'billing' | 'agents.manage' | 'agents.money' | 'catalogue' | 'accounts.remove' | 'admins.manage'

export type Can = (permission: Perm) => boolean

export const ALL_PERMS: Perm[] = ['shops.approve', 'shops.open', 'shops.review', 'support', 'billing', 'agents.manage', 'agents.money', 'catalogue', 'accounts.remove', 'admins.manage']

/** The built-in roles, as the server has them: used until the server's answer arrives. */
export const BUILT_IN: Record<AdminRole, Perm[]> = {
  super: ALL_PERMS,
  approver: ['shops.approve', 'shops.open', 'shops.review', 'catalogue'],
  support: ['support'],
  billing: ['billing', 'shops.open', 'agents.money', 'catalogue'],
  agent_manager: ['agents.manage', 'agents.money', 'shops.review', 'catalogue'],
  custom: [],
}

/** This admin's permissions, read from the server (a custom role's ticks), so screens show only what will work. */
export function usePlatformCan(role: AdminRole): Can {
  const me = useQuery({ queryKey: ['platform', 'me'], queryFn: () => api<{ role: AdminRole; customRoleName: string | null; permissions: Perm[] }>('/v1/platform/me'), staleTime: 60_000 }).data
  const set = new Set(me?.permissions ?? BUILT_IN[role] ?? [])
  return (permission) => set.has(permission)
}
