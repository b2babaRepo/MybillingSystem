import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Aisle } from '../components/AisleTiles'

/** The category tree (sections of aisles), the same for every shop. Open to guests too, for the online store. */
export const useAisles = () =>
  useQuery({ queryKey: ['aisles'], queryFn: () => api<Aisle[]>('/v1/catalogue/aisles'), staleTime: Infinity })
