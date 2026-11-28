import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { Button } from '../components/ui'
import { confirmAsk } from '../lib/ask'
import { notify } from '../lib/notify'
import type { Perm } from '../lib/platformAccess'
import type { AdminRole } from '../lib/types'

export interface CustomRole { id: string; name: string; permissions: Perm[]; admins: number }
export interface RolesData { permissions: Perm[]; grantable: Perm[]; builtIn: { key: AdminRole; permissions: Perm[]; admins: number }[]; custom: CustomRole[] }

export const useRoles = () => useQuery({ queryKey: ['platform', 'roles'], queryFn: () => api<RolesData>('/v1/platform/roles') })

/**
 * Super admins: every role against every permission. Built-in roles are fixed (shown greyed); custom roles are ticked
 * here, and a change applies to their admins on their next click.
 */
export function RolesGrid() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const data = useRoles().data
  const [draft, setDraft] = useState<{ id: string | null; name: string; permissions: Perm[] } | null>(null)
  const [message, setMessage] = useState('')
  if (!data) return null

  async function save() {
    if (!draft) return
    setMessage('')
    try {
      const next = await api<RolesData>(draft.id ? `/v1/platform/roles/${draft.id}` : '/v1/platform/roles', draft.id ? 'PUT' : 'POST', { name: draft.name, permissions: draft.permissions })
      queryClient.setQueryData(['platform', 'roles'], next)
      await queryClient.invalidateQueries({ queryKey: ['platform', 'admins'] })
      setDraft(null)
      notify.success(t('rolesAdmin.saved'))
    } catch (e) {
      notify.error(e)
    }
  }

  async function remove(role: CustomRole) {
    if (!(await confirmAsk(t('rolesAdmin.deleteAsk', { name: role.name })))) return
    setMessage('')
    try {
      queryClient.setQueryData(['platform', 'roles'], await api<RolesData>(`/v1/platform/roles/${role.id}`, 'DELETE'))
      notify.success(t('rolesAdmin.deleted', { name: role.name }))
    } catch (e) {
      notify.error(e)
    }
  }

  const toggle = (p: Perm) => draft && setDraft({ ...draft, permissions: draft.permissions.includes(p) ? draft.permissions.filter((x) => x !== p) : [...draft.permissions, p] })
  const columns = [
    ...data.builtIn.map((b) => ({ key: b.key as string, name: t(`admins.roles.${b.key}`), permissions: b.permissions, admins: b.admins, custom: null as CustomRole | null })),
    ...data.custom.map((c) => ({ key: c.id, name: c.name, permissions: c.permissions, admins: c.admins, custom: c })),
  ]

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <header>
        <h2 className="text-lg font-bold">{t('rolesAdmin.title')}</h2>
        <p className="text-sm text-muted">{t('rolesAdmin.hint')}</p>
      </header>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs">
              <th className="px-2 py-2 text-muted">{t('rolesAdmin.permission')}</th>
              {columns.map((c) => (
                <th key={c.key} className="px-2 py-2 text-center align-bottom">
                  <span className="block font-bold">{c.name}</span>
                  <span className="block font-normal text-muted">{c.custom ? t('rolesAdmin.custom') : t('rolesAdmin.builtIn')} · {t('rolesAdmin.admins', { count: c.admins })}</span>
                  {c.custom && <span className="mt-1 flex justify-center gap-2">
                    <button className="text-brand underline" onClick={() => setDraft({ id: c.custom!.id, name: c.custom!.name, permissions: c.custom!.permissions })}>{t('rolesAdmin.edit')}</button>
                    <button className="text-due underline" onClick={() => void remove(c.custom!)}>{t('rolesAdmin.delete')}</button>
                  </span>}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.permissions.map((p) => (
              <tr key={p} className="border-t border-line">
                <td className="px-2 py-2"><b>{t(`rolesAdmin.perm.${p}`)}</b><br /><span className="text-xs text-muted">{t(`rolesAdmin.permHint.${p}`)}</span></td>
                {columns.map((c) => (
                  <td key={c.key} className={`px-2 py-2 text-center text-lg ${c.custom ? '' : 'opacity-60'}`}>{c.permissions.includes(p) ? '✓' : '·'}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {draft ? (
        <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 ring-2 ring-brand/30">
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('rolesAdmin.name')}
            <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={40} className="h-11 rounded-xl border border-line bg-white px-3 font-normal" />
          </label>
          <div className="grid gap-1 sm:grid-cols-2">
            {data.permissions.map((p) => {
              const grantable = data.grantable.includes(p)
              return (
                <label key={p} className={`flex min-h-11 items-start gap-2 rounded-lg bg-white p-2 ${grantable ? '' : 'opacity-50'}`}>
                  <input type="checkbox" className="mt-1 h-5 w-5" disabled={!grantable} checked={draft.permissions.includes(p)} onChange={() => toggle(p)} />
                  <span><b className="text-sm">{t(`rolesAdmin.perm.${p}`)}</b><br /><span className="text-xs text-muted">{grantable ? t(`rolesAdmin.permHint.${p}`) : t('rolesAdmin.superOnly')}</span></span>
                </label>
              )
            })}
          </div>
          <div className="flex gap-2">
            <Button disabled={draft.name.trim().length < 2 || draft.permissions.length === 0} onClick={() => void save()}>{t('save')}</Button>
            <Button tone="plain" onClick={() => setDraft(null)}>{t('cancel')}</Button>
          </div>
        </div>
      ) : (
        <button onClick={() => setDraft({ id: null, name: '', permissions: [] })} className="min-h-11 self-start rounded-xl border border-brand px-4 font-bold text-brand">+ {t('rolesAdmin.add')}</button>
      )}
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
    </section>
  )
}
