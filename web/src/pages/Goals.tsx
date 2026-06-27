import { useState } from 'react'
import { Plus, Trash2 } from 'lucide-react'
import {
  useContributeGoal,
  useCreateGoal,
  useDeleteGoal,
  useGoals,
} from '../api/hooks'
import type { GoalDto } from '../api/types'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Dialog } from '../components/ui/dialog'
import { Input } from '../components/ui/input'
import { FormField, useZodForm } from '../components/ui/form'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { formatDate, formatMoney } from '../lib/utils'
import {
  contributeSchema,
  goalSchema,
  type ContributeFormValues,
  type GoalFormValues,
} from '../features/goals/schema'

export function Goals() {
  const goals = useGoals()
  const [createOpen, setCreateOpen] = useState(false)
  const [contributeTo, setContributeTo] = useState<GoalDto | null>(null)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Goals"
        subtitle="Save toward what matters — track progress to each target."
        action={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden /> New goal
          </Button>
        }
      />

      {goals.isLoading ? (
        <LoadingState label="Loading goals" />
      ) : goals.isError ? (
        <ErrorState message="Could not load goals." onRetry={() => goals.refetch()} />
      ) : goals.data && goals.data.length > 0 ? (
        <ul className="grid gap-3 sm:grid-cols-2">
          {goals.data.map((goal) => (
            <li key={goal.id}>
              <GoalCard goal={goal} onContribute={() => setContributeTo(goal)} />
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          title="No goals yet"
          description="Create a savings goal and contribute toward it over time."
          action={
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden /> New goal
            </Button>
          }
        />
      )}

      <CreateGoalDialog open={createOpen} onClose={() => setCreateOpen(false)} />
      <ContributeDialog goal={contributeTo} onClose={() => setContributeTo(null)} />
    </div>
  )
}

function GoalCard({ goal, onContribute }: { goal: GoalDto; onContribute: () => void }) {
  const remove = useDeleteGoal()
  const pct = Math.min(100, Math.round(goal.progress * 100))
  return (
    <Card>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-medium">{goal.name}</p>
          {goal.targetDate ? (
            <p className="text-xs text-slate-400">by {formatDate(goal.targetDate)}</p>
          ) : null}
        </div>
        <Button
          variant="ghost"
          size="icon"
          aria-label={`Delete ${goal.name}`}
          onClick={() => remove.mutate(goal.id)}
          disabled={remove.isPending}
        >
          <Trash2 className="h-4 w-4 text-rose-500" aria-hidden />
        </Button>
      </div>

      <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
        <div className="h-full rounded-full bg-violet-600" style={{ width: `${pct}%` }} />
      </div>
      <div className="mt-1 flex items-center justify-between text-xs text-slate-500">
        <span className="tabular-nums">
          {formatMoney(goal.currentAmount)} / {formatMoney(goal.targetAmount)}
        </span>
        <span className="tabular-nums">{pct}%</span>
      </div>

      <Button variant="secondary" size="sm" className="mt-3 w-full" onClick={onContribute}>
        Add contribution
      </Button>
    </Card>
  )
}

function CreateGoalDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const create = useCreateGoal()
  const form = useZodForm(goalSchema, {
    defaultValues: { name: '', targetAmount: undefined, targetDate: '' },
  })

  function submit(values: GoalFormValues) {
    create.mutate(
      {
        name: values.name,
        targetAmount: values.targetAmount,
        targetDate: values.targetDate || null,
      },
      {
        onSuccess: () => {
          form.reset()
          onClose()
        },
      },
    )
  }

  return (
    <Dialog open={open} onClose={onClose} title="New goal" description="Set a name and a target amount.">
      <form className="space-y-3" onSubmit={form.handleSubmit(submit)}>
        <FormField label="Name" error={form.formState.errors.name?.message}>
          {(id) => <Input id={id} placeholder="Emergency fund" {...form.register('name')} />}
        </FormField>
        <FormField label="Target amount" error={form.formState.errors.targetAmount?.message}>
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
        <FormField label="Target date" hint="Optional" error={form.formState.errors.targetDate?.message}>
          {(id) => <Input id={id} type="date" {...form.register('targetDate')} />}
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Saving…' : 'Create goal'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}

function ContributeDialog({ goal, onClose }: { goal: GoalDto | null; onClose: () => void }) {
  const contribute = useContributeGoal()
  const form = useZodForm(contributeSchema, { defaultValues: { amount: undefined } })

  function submit(values: ContributeFormValues) {
    if (!goal) {
      return
    }
    contribute.mutate(
      { id: goal.id, body: { amount: values.amount } },
      {
        onSuccess: () => {
          form.reset()
          onClose()
        },
      },
    )
  }

  return (
    <Dialog
      key={goal?.id ?? 'none'}
      open={!!goal}
      onClose={onClose}
      title={goal ? `Contribute to ${goal.name}` : 'Contribute'}
    >
      <form className="space-y-3" onSubmit={form.handleSubmit(submit)}>
        <FormField label="Amount" error={form.formState.errors.amount?.message}>
          {(id) => (
            <Input
              id={id}
              type="number"
              step="0.01"
              min="0"
              inputMode="decimal"
              autoFocus
              placeholder="0.00"
              {...form.register('amount')}
            />
          )}
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={contribute.isPending}>
            {contribute.isPending ? 'Saving…' : 'Add'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
