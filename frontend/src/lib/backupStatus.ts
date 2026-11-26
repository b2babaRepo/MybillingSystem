import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { BackupStatus } from '../screens/BackupScreen'

export const useBackupStatus = (shopId: string) =>
  useQuery({ queryKey: [shopId, 'backup-status'], queryFn: () => api<BackupStatus>('/v1/shop/backup-status'), staleTime: 60_000, retry: false })
