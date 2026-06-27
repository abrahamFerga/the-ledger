import { useState } from 'react'
import { Plus } from 'lucide-react'
import { useBudgets, useCategories, useSetBudget } from '../api/hooks'
import type { BudgetStatusDto } from '../api/types'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Dialog } from '../components/ui/dialog'
import { Input } from '../components/ui/input'
import { Select } from '../components/ui/select'
import { MonthPicker } from '../components/ui/month-picker'
import { FormField, useZodForm } from '../components/ui/form'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { formatMoney } from '../lib/utils'
import { budgetSchema, type BudgetFormValues } from '../features/budgets/schema'

export function Budgets() {
  const now = new Date()
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 })
  const [editing, setEditing] = useState<BudgetStatusDto | null>(null)
  const [open, setOpen] = useState(false)

  const budgets = useBudgets(period.year, period.month)

  function openCreate() {
    setEditing(null)
    setOpen(true)
  }

  function openEdit(budget: BudgetStatusDto) {
    setEditing(budget)
    setOpen(true)
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Budgets"
        subtitle="Set a monthly target per category and track spending against it."
        action={
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4" aria-hidden /> New budget
          </Button>
        }
      />

      <MonthPicker
        year={period.year}
        month={period.month}
        onChange={(year, month) => setPeriod({ year, month })}
      />

      {budgets.isLoading ? (
        <LoadingState label="Loading budgets" />
      ) : budgets.isError ? (
        <ErrorState message="Could not load budgets." onRetry={() => budgets.refetch()} />
      ) : budgets.data && budgets.data.length > 0 ? (
        <ul className="space-y-2">
          {budgets.data.map((budget) => (
            <li key={budget.categoryId}>
              <BudgetRow budget={budget} onEdit={() => openEdit(budget)} />
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          title="No budgets this month"
          description="Create a category target to start tracking spent-vs-target."
          action={
            <Button onClick={openCreate}>
              <Plus className="h-4 w-4" aria-hidden /> New budget
            </Button>
          }
        />
      )}

      <BudgetDialog
        open={open}
        onClose={() => setOpen(false)}
        period={period}
        editing={editing}
      />
    </div>
  )
}

function BudgetRow({ budget, onEdit }: { budget: BudgetStatusDto; onEdit: () => void }) {
  const cap = budget.target + budget.rolledOver
  const pct = cap > 0 ? Math.min(100, Math.round((budget.spent / cap) * 100)) : 0
  const over = budget.remaining < 0
  return (
    <Card>
      <div className="mb-1 flex items-center justify-between text-sm">
        <button type="button" className="font-medium hover:text-violet-700" onClick={onEdit}>
          {budget.categoryName ?? 'Category'}
        </button>
        <span className="tabular-nums text-slate-500">
          {formatMoney(budget.spent)} / {formatMoney(cap)}
        </span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
        <div
          className={`h-full rounded-full ${over ? 'bg-rose-500' : 'bg-violet-600'}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      <p className={`mt-1 text-xs ${over ? 'text-rose-600' : 'text-slate-400'}`}>
        {over
          ? `${formatMoney(Math.abs(budget.remaining))} over budget`
          : `${formatMoney(budget.remaining)} remaining`}
        {budget.rollover ? ' · rollover on' : ''}
      </p>
    </Card>
  )
}

function BudgetDialog({
  open,
  onClose,
  period,
  editing,
}: {
  open: boolean
  onClose: () => void
  period: { year: number; month: number }
  editing: BudgetStatusDto | null
}) {
  const categories = useCategories()
  const setBudget = useSetBudget()
  const form = useZodForm(budgetSchema, {
    // Re-seeded on open via the key remount below, so defaults reflect create vs. edit.
    defaultValues: {
      categoryId: editing?.categoryId ?? '',
      targetAmount: editing?.target,
      rollover: editing?.rollover ?? false,
    },
  })

  function submit(values: BudgetFormValues) {
    setBudget.mutate(
      {
        categoryId: values.categoryId,
        year: period.year,
        month: period.month,
        targetAmount: values.targetAmount,
        rollover: values.rollover,
      },
      {
        onSuccess: () => {
          form.reset()
          onClose()
        },
      },
    )
  }

  // Remount the form when the editing target changes so defaultValues re-seed.
  return (
    <Dialog
      key={editing?.categoryId ?? 'new'}
      open={open}
      onClose={onClose}
      title={editing ? 'Edit budget' : 'New budget'}
      description="Targets apply to the selected month."
    >
      <form className="space-y-3" onSubmit={form.handleSubmit(submit)}>
        <FormField label="Category" error={form.formState.errors.categoryId?.message}>
          {(id) => (
            <Select id={id} disabled={!!editing} {...form.register('categoryId')}>
              <option value="">Select a category…</option>
              {(categories.data ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          )}
        </FormField>

        <FormField label="Monthly target" error={form.formState.errors.targetAmount?.message}>
          {(id) => (
            <Input
              id={id}
              type="number"
              step="0.01"
              min="0"
              inputMode="decimal"
              placeholder="0.00"
              {...form.register('targetAmount')}
            />
          )}
        </FormField>

        <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-slate-300 text-violet-600 focus:ring-violet-500"
            {...form.register('rollover')}
          />
          Roll over unused budget to next month
        </label>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={setBudget.isPending}>
            {setBudget.isPending ? 'Saving…' : 'Save budget'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
