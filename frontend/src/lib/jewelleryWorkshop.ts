import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { type JewelleryPiece } from './jewellery'
import type { Scale, StoneDraft } from '../screens/JewelleryWorkshopPanels'

export function useScales(shopId: string) {
  return useQuery({ queryKey: [shopId, 'jewellery', 'scales'], queryFn: () => api<Scale[]>('/v1/jewellery/scales') }).data ?? []
}

export const blankStone: StoneDraft = { kind: 'Diamond', labGrown: false, count: '1', carat: '', shape: '', colour: '', clarity: '', certLab: '', certNo: '', ratePerCarat: '' }

export const stoneToDraft = (s: NonNullable<JewelleryPiece['stones']>[number]): StoneDraft => ({ kind: s.kind, labGrown: s.labGrown, count: String(s.count), carat: s.carat ? String(s.carat) : '', shape: s.shape ?? '',
  colour: s.colour ?? '', clarity: s.clarity ?? '', certLab: s.certLab ?? '', certNo: s.certNo ?? '', ratePerCarat: s.ratePerCarat ? String(s.ratePerCarat) : '' })

export const stoneBody = (s: StoneDraft) => ({ kind: s.kind.trim() || 'Stone', labGrown: s.labGrown, count: Number(s.count) || 1, carat: Number(s.carat) || 0, shape: s.shape || null, colour: s.colour || null,
  clarity: s.clarity || null, certLab: s.certLab || null, certNo: s.certNo || null, ratePerCarat: Number(s.ratePerCarat) || 0 })
