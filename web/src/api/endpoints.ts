/**
 * Typed wrappers over the API endpoint surface. Each function maps to exactly one route in
 * `src/TheLedger.Api/Endpoints/*.cs`. Paths are relative to the API base (`/api/v1/...`).
 */
import { request, requestBlob } from './client'
import type {
  AccountDto,
  AlertDto,
  BudgetStatusDto,
  CategoryDto,
  ContributeRequest,
  CreateAccountRequest,
  CreateCategoryRequest,
  CreateGoalRequest,
  GoalDto,
  HouseholdDto,
  ImportCsvRequest,
  ManualTransactionRequest,
  MemberDto,
  MonthlyTotalDto,
  NetWorthDto,
  QuickAddRequest,
  RawTransactionDraft,
  ReceiptDto,
  SetBudgetRequest,
  CategorySpendDto,
  StatementDto,
  TransactionDirection,
  TransactionDraft,
  TransactionDto,
  TransactionFeedQuery,
  TransactionListItem,
  UpdateTransactionRequest,
  WhatsAppOptInDto,
  WhatsAppOptInRequest,
} from './types'

const V1 = '/api/v1'

// --- Foundations: households + members ---
export const householdsApi = {
  current: () => request<HouseholdDto>(`${V1}/households/current`),
  members: () => request<MemberDto[]>(`${V1}/members`),
}

// --- Accounts (ingestion) ---
export const accountsApi = {
  list: () => request<AccountDto[]>(`${V1}/accounts`),
  create: (body: CreateAccountRequest) =>
    request<AccountDto>(`${V1}/accounts`, { method: 'POST', body }),
}

// --- Transactions: unified ledger feed + manual add + edit + review queue ---
export const transactionsApi = {
  feed: (query: TransactionFeedQuery) =>
    request<TransactionListItem[]>(`${V1}/ledger`, {
      query: {
        accountId: query.accountId,
        categoryId: query.categoryId,
        confirmedOnly: query.confirmedOnly,
      },
    }),
  addManual: (body: ManualTransactionRequest) =>
    request<TransactionDto>(`${V1}/transactions`, { method: 'POST', body }),
  update: (id: string, body: UpdateTransactionRequest) =>
    request<TransactionListItem>(`${V1}/transactions/${id}`, { method: 'PATCH', body }),
  reviewQueue: (statementId?: string) =>
    request<TransactionDto[]>(`${V1}/transactions/review`, {
      query: { statementId },
    }),
}

// --- Categories ---
export const categoriesApi = {
  list: () => request<CategoryDto[]>(`${V1}/categories`),
  create: (body: CreateCategoryRequest) =>
    request<CategoryDto>(`${V1}/categories`, { method: 'POST', body }),
}

// --- Statements: upload (CSV/PDF) + review-and-confirm ---
export const statementsApi = {
  importCsv: (body: ImportCsvRequest) =>
    request<StatementDto>(`${V1}/statements/csv`, { method: 'POST', body }),
  uploadPdf: (accountId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    form.append('accountId', accountId)
    return request<StatementDto>(`${V1}/statements/pdf`, { method: 'POST', rawBody: form })
  },
  confirm: (statementId: string) =>
    request<StatementDto>(`${V1}/statements/${statementId}/confirm`, { method: 'POST' }),
}

// --- Budgets ---
export const budgetsApi = {
  list: (year: number, month: number) =>
    request<BudgetStatusDto[]>(`${V1}/budgets`, { query: { year, month } }),
  set: (body: SetBudgetRequest) =>
    request<BudgetStatusDto>(`${V1}/budgets`, { method: 'POST', body }),
}

// --- Goals ---
export const goalsApi = {
  list: () => request<GoalDto[]>(`${V1}/goals`),
  create: (body: CreateGoalRequest) =>
    request<GoalDto>(`${V1}/goals`, { method: 'POST', body }),
  contribute: (id: string, body: ContributeRequest) =>
    request<GoalDto>(`${V1}/goals/${id}/contribute`, { method: 'POST', body }),
  remove: (id: string) => request<void>(`${V1}/goals/${id}`, { method: 'DELETE' }),
}

// --- Insights + CSV export ---
export const insightsApi = {
  netWorth: () => request<NetWorthDto>(`${V1}/insights/net-worth`),
  spending: (year: number, month: number) =>
    request<CategorySpendDto[]>(`${V1}/insights/spending`, { query: { year, month } }),
  monthly: () => request<MonthlyTotalDto[]>(`${V1}/insights/monthly`),
  exportCsv: () => requestBlob(`${V1}/export/transactions.csv`),
}

// --- Alerts ---
export const alertsApi = {
  list: (includeResolved = false) =>
    request<AlertDto[]>(`${V1}/alerts`, { query: { includeResolved } }),
  dismiss: (id: string) =>
    request<void>(`${V1}/alerts/${id}/dismiss`, { method: 'POST' }),
  scan: () => request<{ raised: number }>(`${V1}/alerts/scan`, { method: 'POST' }),
}

// --- Capture: NL quick-add (epic 9) ---
/** Map the raw wire draft (numeric direction) onto the canonical string-direction draft. */
function normalizeDraft(raw: RawTransactionDraft): TransactionDraft {
  const direction: TransactionDirection = raw.direction === 1 ? 'Credit' : 'Debit'
  return {
    amount: raw.amount,
    currency: raw.currency,
    date: raw.date,
    direction,
    merchant: raw.merchant,
    proposedCategoryId: raw.proposedCategoryId,
    confidence: raw.confidence,
  }
}

export const quickAddApi = {
  /** Parse a free-text phrase into a transaction draft. Never persists — the SPA confirms first. */
  parse: async (body: QuickAddRequest): Promise<TransactionDraft> => {
    const raw = await request<RawTransactionDraft>(`${V1}/transactions/quick-add`, {
      method: 'POST',
      body,
    })
    return normalizeDraft(raw)
  },
}

// --- Capture: receipt OCR (epic 9) ---
export const receiptsApi = {
  list: () => request<ReceiptDto[]>(`${V1}/receipts`),
  /** Upload a receipt photo (multipart) → 202 Accepted with the queued ReceiptDto (OCR pending). */
  upload: (accountId: string, file: File) => {
    const form = new FormData()
    form.append('accountId', accountId)
    form.append('file', file)
    return request<ReceiptDto>(`${V1}/receipts`, { method: 'POST', rawBody: form })
  },
}

// --- Capture: WhatsApp opt-in (epic 9) ---
export const whatsappApi = {
  list: () => request<WhatsAppOptInDto[]>(`${V1}/connectors/whatsapp/opt-in`),
  optIn: (body: WhatsAppOptInRequest) =>
    request<WhatsAppOptInDto>(`${V1}/connectors/whatsapp/opt-in`, { method: 'POST', body }),
  revoke: (contactId: string) =>
    request<void>(`${V1}/connectors/whatsapp/opt-in/${contactId}`, { method: 'DELETE' }),
}
