/**
 * TypeScript mirrors of the backend DTOs. Shapes are copied 1:1 from the .NET records in
 * `src/TheLedger.Application.*` and the minimal-API endpoint groups in
 * `src/TheLedger.Api/Endpoints/*.cs` — keep them in sync with the C# source, do not invent fields.
 *
 * Money is `decimal(19,4)` server-side, serialized as a JSON number (System.Text.Json) — represented
 * here as `number`. Dates are `DateOnly` ("YYYY-MM-DD") strings; timestamps are ISO-8601 strings.
 */

// --- Enums (string-serialized by System.Text.Json default) ---
export const ACCOUNT_TYPES = ['Checking', 'Savings', 'Card', 'Cash'] as const
export type AccountType = (typeof ACCOUNT_TYPES)[number]

export const TRANSACTION_DIRECTIONS = ['Debit', 'Credit'] as const
export type TransactionDirection = (typeof TRANSACTION_DIRECTIONS)[number]

export const CATEGORY_KINDS = ['Income', 'Expense', 'Transfer'] as const
export type CategoryKind = (typeof CATEGORY_KINDS)[number]

// --- Foundations / households ---
export interface HouseholdDto {
  id: string
  name: string
  plan: string
  createdAt: string
}

export interface MemberDto {
  id: string
  email: string
  displayName: string | null
  role: string
}

// --- Ingestion: accounts, manual entry, review queue ---
export interface AccountDto {
  id: string
  name: string
  type: AccountType
  institution: string | null
  currency: string
  maskedNumber: string | null
  currentBalance: number
}

export interface CreateAccountRequest {
  name: string
  type: AccountType
  institution?: string | null
  currency?: string | null
  number?: string | null
}

export interface ManualTransactionRequest {
  accountId: string
  date: string // DateOnly "YYYY-MM-DD"
  description: string
  amount: number
  direction: TransactionDirection
}

/** Returned by ingestion endpoints (manual add / review queue). */
export interface TransactionDto {
  id: string
  accountId: string
  statementId: string | null
  date: string
  description: string
  amount: number
  currency: string
  direction: TransactionDirection
  isConfirmed: boolean
}

export interface StatementDto {
  id: string
  accountId: string
  source: string
  status: string
  transactionCount: number
}

export interface ImportCsvRequest {
  accountId: string
  fileName: string
  content: string
}

// --- Ledger: unified feed, categories ---
export interface TransactionListItem {
  id: string
  accountId: string
  date: string
  description: string
  amount: number
  currency: string
  direction: TransactionDirection
  categoryId: string | null
  categoryName: string | null
  isConfirmed: boolean
  attributedUserId: string | null
}

export interface UpdateTransactionRequest {
  description?: string | null
  categoryId?: string | null
}

export interface CategoryDto {
  id: string
  name: string
  kind: CategoryKind
  isSystem: boolean
}

export interface CreateCategoryRequest {
  name: string
  kind: CategoryKind
}

export interface TransactionFeedQuery {
  accountId?: string
  categoryId?: string
  confirmedOnly?: boolean
}

// --- Budgeting ---
export interface SetBudgetRequest {
  categoryId: string
  year: number
  month: number
  targetAmount: number
  rollover: boolean
}

export interface BudgetStatusDto {
  categoryId: string
  categoryName: string | null
  year: number
  month: number
  target: number
  rolledOver: number
  spent: number
  remaining: number
  rollover: boolean
}

// --- Goals ---
export interface CreateGoalRequest {
  name: string
  targetAmount: number
  targetDate?: string | null
  linkedAccountId?: string | null
}

export interface ContributeRequest {
  amount: number
}

export interface GoalDto {
  id: string
  name: string
  targetAmount: number
  currentAmount: number
  progress: number
  targetDate: string | null
  linkedAccountId: string | null
}

// --- Insights ---
export interface AccountBalanceDto {
  accountId: string
  name: string
  type: string
  balance: number
  currency: string
}

export interface NetWorthDto {
  total: number
  accounts: AccountBalanceDto[]
}

export interface CategorySpendDto {
  categoryId: string | null
  categoryName: string
  total: number
}

export interface MonthlyTotalDto {
  year: number
  month: number
  income: number
  expense: number
  net: number
}

// --- Alerts ---
export interface AlertDto {
  id: string
  type: string
  transactionId: string | null
  accountId: string | null
  message: string
  status: string
  createdAt: string
}
