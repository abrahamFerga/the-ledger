import { useMemo, useState } from 'react'
import { type ColumnDef } from '@tanstack/react-table'
import { Check, Pencil, Plus, X } from 'lucide-react'
import {
  useAccounts,
  useAddManualTransaction,
  useCategories,
  useLedger,
  useUpdateTransaction,
} from '../api/hooks'
import { TRANSACTION_DIRECTIONS, type TransactionListItem } from '../api/types'
import { PageHeader } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Dialog } from '../components/ui/dialog'
import { Input } from '../components/ui/input'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { DataTable } from '../components/ui/data-table'
import { FormField, useZodForm } from '../components/ui/form'
import { EmptyState, ErrorState } from '../components/ui/states'
import { formatDate, formatMoney, todayIso } from '../lib/utils'
import {
  manualTransactionSchema,
  type ManualTransactionFormValues,
} from '../features/transactions/schema'

export function Transactions() {
  const [accountId, setAccountId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [confirmedOnly, setConfirmedOnly] = useState(false)
  const [addOpen, setAddOpen] = useState(false)

  const accounts = useAccounts()
  const categories = useCategories()
  const ledger = useLedger({
    accountId: accountId || undefined,
    categoryId: categoryId || undefined,
    confirmedOnly,
  })
  const update = useUpdateTransaction()

  const columns = useMemo<ColumnDef<TransactionListItem, unknown>[]>(
    () => [
      {
        accessorKey: 'date',
        header: 'Date',
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-slate-500">{formatDate(row.original.date)}</span>
        ),
      },
      {
        accessorKey: 'description',
        header: 'Description',
        cell: ({ row }) => (
          <EditableDescription
            tx={row.original}
            onSave={(description) =>
              update.mutate({ id: row.original.id, body: { description } })
            }
          />
        ),
      },
      {
        accessorKey: 'categoryName',
        header: 'Category',
        cell: ({ row }) => (
          <CategoryPicker
            tx={row.original}
            categories={categories.data ?? []}
            onChange={(catId) => update.mutate({ id: row.original.id, body: { categoryId: catId } })}
          />
        ),
      },
      {
        accessorKey: 'amount',
        header: 'Amount',
        cell: ({ row }) => {
          const isCredit = row.original.direction === 'Credit'
          return (
            <span
              className={`whitespace-nowrap font-medium tabular-nums ${
                isCredit ? 'text-emerald-600' : 'text-slate-900 dark:text-slate-100'
              }`}
            >
              {isCredit ? '+' : '−'}
              {formatMoney(row.original.amount, row.original.currency)}
            </span>
          )
        },
      },
      {
        id: 'status',
        header: 'Status',
        enableSorting: false,
        cell: ({ row }) =>
          row.original.isConfirmed ? (
            <Badge tone="green">Confirmed</Badge>
          ) : (
            <Badge tone="amber">Staged</Badge>
          ),
      },
    ],
    [categories.data, update],
  )

  return (
    <div className="space-y-4">
      <PageHeader
        title="Ledger"
        subtitle="Every transaction across your accounts. Edit a description or category inline."
        action={
          <Button onClick={() => setAddOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden /> Add
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Select
          aria-label="Filter by account"
          value={accountId}
          onChange={(e) => setAccountId(e.target.value)}
          className="w-auto"
        >
          <option value="">All accounts</option>
          {(accounts.data ?? []).map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </Select>
        <Select
          aria-label="Filter by category"
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value)}
          className="w-auto"
        >
          <option value="">All categories</option>
          {(categories.data ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <label className="flex items-center gap-1.5 text-sm text-slate-600 dark:text-slate-300">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-slate-300 text-violet-600 focus:ring-violet-500"
            checked={confirmedOnly}
            onChange={(e) => setConfirmedOnly(e.target.checked)}
          />
          Confirmed only
        </label>
      </div>

      {ledger.isError ? (
        <ErrorState message="Could not load the ledger." onRetry={() => ledger.refetch()} />
      ) : (
        <DataTable
          columns={columns}
          data={ledger.data ?? []}
          isLoading={ledger.isLoading}
          searchPlaceholder="Search descriptions…"
          emptyState={
            <EmptyState
              title="No transactions"
              description="Add one manually, or upload a statement to import a batch."
              action={
                <Button onClick={() => setAddOpen(true)}>
                  <Plus className="h-4 w-4" aria-hidden /> Add transaction
                </Button>
              }
            />
          }
        />
      )}

      <AddTransactionDialog open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  )
}

function EditableDescription({
  tx,
  onSave,
}: {
  tx: TransactionListItem
  onSave: (description: string) => void
}) {
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState(tx.description)

  if (!editing) {
    return (
      <button
        type="button"
        className="group inline-flex items-center gap-1 text-left hover:text-violet-700"
        onClick={() => {
          setValue(tx.description)
          setEditing(true)
        }}
      >
        <span className="max-w-[14rem] truncate">{tx.description}</span>
        <Pencil className="h-3 w-3 opacity-0 group-hover:opacity-60" aria-hidden />
      </button>
    )
  }

  return (
    <div className="flex items-center gap-1">
      <Input
        aria-label="Edit description"
        value={value}
        autoFocus
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            onSave(value.trim())
            setEditing(false)
          } else if (e.key === 'Escape') {
            setEditing(false)
          }
        }}
        className="h-8 py-1"
      />
      <Button
        size="icon"
        aria-label="Save description"
        onClick={() => {
          onSave(value.trim())
          setEditing(false)
        }}
      >
        <Check className="h-4 w-4" aria-hidden />
      </Button>
      <Button variant="secondary" size="icon" aria-label="Cancel edit" onClick={() => setEditing(false)}>
        <X className="h-4 w-4" aria-hidden />
      </Button>
    </div>
  )
}

function CategoryPicker({
  tx,
  categories,
  onChange,
}: {
  tx: TransactionListItem
  categories: { id: string; name: string }[]
  onChange: (categoryId: string | null) => void
}) {
  return (
    <Select
      aria-label={`Category for ${tx.description}`}
      value={tx.categoryId ?? ''}
      onChange={(e) => onChange(e.target.value || null)}
      className="h-8 w-auto min-w-[8rem] py-1 text-xs"
    >
      <option value="">Uncategorized</option>
      {categories.map((c) => (
        <option key={c.id} value={c.id}>
          {c.name}
        </option>
      ))}
    </Select>
  )
}

function AddTransactionDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const accounts = useAccounts()
  const add = useAddManualTransaction()
  const form = useZodForm(manualTransactionSchema, {
    defaultValues: {
      accountId: '',
      date: todayIso(),
      description: '',
      amount: undefined,
      direction: 'Debit',
    },
  })

  function submit(values: ManualTransactionFormValues) {
    add.mutate(
      {
        accountId: values.accountId,
        date: values.date,
        description: values.description,
        amount: values.amount,
        direction: values.direction,
      },
      {
        onSuccess: () => {
          form.reset({ ...form.getValues(), description: '', amount: undefined })
          onClose()
        },
      },
    )
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Add transaction"
      description="Manually record a transaction in the ledger."
    >
      <form className="space-y-3" onSubmit={form.handleSubmit(submit)}>
        <FormField label="Account" error={form.formState.errors.accountId?.message}>
          {(id) => (
            <Select id={id} {...form.register('accountId')}>
              <option value="">Select an account…</option>
              {(accounts.data ?? []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </Select>
          )}
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Date" error={form.formState.errors.date?.message}>
            {(id) => <Input id={id} type="date" {...form.register('date')} />}
          </FormField>
          <FormField label="Direction" error={form.formState.errors.direction?.message}>
            {(id) => (
              <Select id={id} {...form.register('direction')}>
                {TRANSACTION_DIRECTIONS.map((d) => (
                  <option key={d} value={d}>
                    {d === 'Debit' ? 'Expense (Debit)' : 'Income (Credit)'}
                  </option>
                ))}
              </Select>
            )}
          </FormField>
        </div>

        <FormField label="Description" error={form.formState.errors.description?.message}>
          {(id) => <Input id={id} placeholder="OXXO, Nómina, …" {...form.register('description')} />}
        </FormField>

        <FormField label="Amount" error={form.formState.errors.amount?.message}>
          {(id) => (
            <Input
              id={id}
              type="number"
              step="0.01"
              min="0"
              inputMode="decimal"
              placeholder="0.00"
              {...form.register('amount')}
            />
          )}
        </FormField>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={add.isPending}>
            {add.isPending ? 'Saving…' : 'Add transaction'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
