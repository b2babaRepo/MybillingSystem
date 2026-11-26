import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Dispute } from '../screens/DisputesScreen'

export const useOpenDisputes = () => useQuery({ queryKey: ['disputes'], queryFn: () => api<Dispute[]>('/v1/disputes'), staleTime: 60_000 })
