/**
 * TanStack Query hooks — the data layer the pages consume. Queries fetch server state; mutations
 * write through the typed endpoints, apply optimistic updates where it improves perceived latency,
 * invalidate affected queries, and surface ApiError Problem Details as toasts.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  accountsApi,
  alertsApi,
  budgetsApi,
  categoriesApi,
  goalsApi,
  householdsApi,
  insightsApi,
  statementsApi,
  transactionsApi,
} from './endpoints'
import { ApiError } from './client'
import { queryKeys } from './queryClient'
import { useToast } from '../components/ui/toast'
import type {
  AccountDto,
  AlertDto,
  ContributeRequest,
  CreateAccountRequest,
  CreateGoalRequest,
  GoalDto,
  ImportCsvRequest,
  ManualTransactionRequest,
  SetBudgetRequest,
  TransactionFeedQuery,
  TransactionListItem,
  UpdateTransactionRequest,
} from './types'

/** Convert any thrown error to a toast-friendly message. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.displayMessage
  }
  if (error instanceof Error) {
    return error.message
  }
  return 'Something went wrong'
}

// --- Household + members ---
export function useHousehold() {
  return useQuery({ queryKey: queryKeys.household, queryFn: householdsApi.current, retry: false })
}

export function useMembers() {
  return useQuery({ queryKey: queryKeys.members, queryFn: householdsApi.members })
}

// --- Accounts ---
export function useAccounts() {
  return useQuery({ queryKey: queryKeys.accounts, queryFn: accountsApi.list })
}

export function useCreateAccount() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (body: CreateAccountRequest) => accountsApi.create(body),
    onSuccess: (account: AccountDto) => {
      qc.invalidateQueries({ queryKey: queryKeys.accounts })
      qc.invalidateQueries({ queryKey: queryKeys.netWorth })
      toast.success(`Account "${account.name}" added`)
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

// --- Categories ---
export function useCategories() {
  return useQuery({ queryKey: queryKeys.categories, queryFn: categoriesApi.list })
}

// --- Transactions / ledger ---
export function useLedger(query: TransactionFeedQuery) {
  const params = {
    accountId: query.accountId ?? null,
    categoryId: query.categoryId ?? null,
    confirmedOnly: query.confirmedOnly ?? true,
  }
  return useQuery({
    queryKey: queryKeys.ledger(params),
    queryFn: () => transactionsApi.feed(query),
  })
}

export function useAddManualTransaction() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (body: ManualTransactionRequest) => transactionsApi.addManual(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['ledger'] })
      qc.invalidateQueries({ queryKey: queryKeys.accounts })
      qc.invalidateQueries({ queryKey: queryKeys.netWorth })
      toast.success('Transaction added')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

/**
 * Inline edit / recategorize a transaction with an optimistic update against every cached ledger
 * page. Rolls back on error.
 */
export function useUpdateTransaction() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTransactionRequest }) =>
      transactionsApi.update(id, body),
    onMutate: async ({ id, body }) => {
      await qc.cancelQueries({ queryKey: ['ledger'] })
      const snapshots = qc.getQueriesData<TransactionListItem[]>({ queryKey: ['ledger'] })
      for (const [key, data] of snapshots) {
        if (!data) {
          continue
        }
        qc.setQueryData<TransactionListItem[]>(
          key,
          data.map((t) =>
            t.id === id
              ? {
                  ...t,
                  description: body.description ?? t.description,
                  categoryId: body.categoryId !== undefined ? body.categoryId : t.categoryId,
                }
              : t,
          ),
        )
      }
      return { snapshots }
    },
    onError: (e, _vars, context) => {
      context?.snapshots.forEach(([key, data]) => qc.setQueryData(key, data))
      toast.error(errorMessage(e))
    },
    onSuccess: () => toast.success('Transaction updated'),
    onSettled: () => qc.invalidateQueries({ queryKey: ['ledger'] }),
  })
}

// --- Review queue (staged transactions) ---
export function useReviewQueue(statementId?: string) {
  return useQuery({
    queryKey: queryKeys.reviewQueue(statementId),
    queryFn: () => transactionsApi.reviewQueue(statementId),
  })
}

export function useImportCsv() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (body: ImportCsvRequest) => statementsApi.importCsv(body),
    onSuccess: (statement) => {
      qc.invalidateQueries({ queryKey: ['reviewQueue'] })
      toast.success(`Imported ${statement.transactionCount} transaction(s) for review`)
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

export function useUploadPdf() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: ({ accountId, file }: { accountId: string; file: File }) =>
      statementsApi.uploadPdf(accountId, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reviewQueue'] })
      toast.success('Statement uploaded — staged transactions are ready to review')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

export function useConfirmStatement() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (statementId: string) => statementsApi.confirm(statementId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reviewQueue'] })
      qc.invalidateQueries({ queryKey: ['ledger'] })
      qc.invalidateQueries({ queryKey: queryKeys.netWorth })
      toast.success('Staged transactions confirmed')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

// --- Budgets ---
export function useBudgets(year: number, month: number) {
  return useQuery({
    queryKey: queryKeys.budgets(year, month),
    queryFn: () => budgetsApi.list(year, month),
  })
}

export function useSetBudget() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (body: SetBudgetRequest) => budgetsApi.set(body),
    onSuccess: (_data, vars) => {
      qc.invalidateQueries({ queryKey: queryKeys.budgets(vars.year, vars.month) })
      toast.success('Budget saved')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

// --- Goals ---
export function useGoals() {
  return useQuery({ queryKey: queryKeys.goals, queryFn: goalsApi.list })
}

export function useCreateGoal() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (body: CreateGoalRequest) => goalsApi.create(body),
    onSuccess: (goal: GoalDto) => {
      qc.invalidateQueries({ queryKey: queryKeys.goals })
      toast.success(`Goal "${goal.name}" created`)
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

export function useContributeGoal() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: ContributeRequest }) =>
      goalsApi.contribute(id, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.goals })
      toast.success('Contribution added')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

export function useDeleteGoal() {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (id: string) => goalsApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.goals })
      toast.success('Goal removed')
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
}

// --- Insights ---
export function useNetWorth() {
  return useQuery({ queryKey: queryKeys.netWorth, queryFn: insightsApi.netWorth })
}

export function useSpending(year: number, month: number) {
  return useQuery({
    queryKey: queryKeys.spending(year, month),
    queryFn: () => insightsApi.spending(year, month),
  })
}

export function useMonthlyTotals() {
  return useQuery({ queryKey: queryKeys.monthly, queryFn: insightsApi.monthly })
}

// --- Alerts ---
export function useAlerts(includeResolved = false) {
  return useQuery({
    queryKey: queryKeys.alerts(includeResolved),
    queryFn: () => alertsApi.list(includeResolved),
  })
}

/** Dismiss (mark seen) an alert, optimistically removing it from the open list. */
export function useDismissAlert(includeResolved = false) {
  const qc = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: (id: string) => alertsApi.dismiss(id),
    onMutate: async (id) => {
      const key = queryKeys.alerts(includeResolved)
      await qc.cancelQueries({ queryKey: key })
      const previous = qc.getQueryData<AlertDto[]>(key)
      if (previous) {
        qc.setQueryData<AlertDto[]>(key, previous.filter((a) => a.id !== id))
      }
      return { previous, key }
    },
    onError: (e, _id, context) => {
      if (context?.previous) {
        qc.setQueryData(context.key, context.previous)
      }
      toast.error(errorMessage(e))
    },
    onSuccess: () => toast.success('Alert dismissed'),
    onSettled: () => qc.invalidateQueries({ queryKey: ['alerts'] }),
  })
}
