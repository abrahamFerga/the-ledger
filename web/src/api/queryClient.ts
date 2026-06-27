/** Shared TanStack Query client + the canonical query-key factory. */
import { QueryClient } from '@tanstack/react-query'

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: 1,
        staleTime: 30_000,
        refetchOnWindowFocus: false,
      },
    },
  })
}

/** Centralized query keys so invalidation stays consistent across hooks. */
export const queryKeys = {
  household: ['household'] as const,
  members: ['members'] as const,
  accounts: ['accounts'] as const,
  categories: ['categories'] as const,
  ledger: (params: Record<string, unknown>) => ['ledger', params] as const,
  reviewQueue: (statementId?: string) => ['reviewQueue', statementId ?? null] as const,
  budgets: (year: number, month: number) => ['budgets', year, month] as const,
  goals: ['goals'] as const,
  netWorth: ['insights', 'netWorth'] as const,
  spending: (year: number, month: number) => ['insights', 'spending', year, month] as const,
  monthly: ['insights', 'monthly'] as const,
  alerts: (includeResolved: boolean) => ['alerts', includeResolved] as const,
}
