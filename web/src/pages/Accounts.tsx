import { useState } from 'react'
import { Plus } from 'lucide-react'
import { useAccounts, useCreateAccount } from '../api/hooks'
import { ACCOUNT_TYPES, type AccountType } from '../api/types'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Dialog } from '../components/ui/dialog'
import { Input } from '../components/ui/input'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { FormField, useZodForm } from '../components/ui/form'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { formatMoney } from '../lib/utils'
import { accountSchema, type AccountFormValues } from '../features/accounts/schema'

const TYPE_TONE: Record<AccountType, 'violet' | 'green' | 'amber' | 'neutral'> = {
  Checking: 'violet',
  Savings: 'green',
  Card: 'amber',
  Cash: 'neutral',
}

export function Accounts() {
  const accounts = useAccounts()
  const [open, setOpen] = useState(false)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Accounts"
        subtitle="Checking, savings, cards, and cash across your household."
        action={
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden /> Add
          </Button>
        }
      />

      {accounts.isLoading ? (
        <LoadingState label="Loading accounts" />
      ) : accounts.isError ? (
        <ErrorState message="Could not load accounts." onRetry={() => accounts.refetch()} />
      ) : accounts.data && accounts.data.length > 0 ? (
        <ul className="space-y-2">
          {accounts.data.map((account) => (
            <li key={account.id}>
              <Card className="flex items-center justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate font-medium">{account.name}</span>
                    <Badge tone={TYPE_TONE[account.type]}>{account.type}</Badge>
                  </div>
                  <p className="mt-0.5 truncate text-xs text-slate-500">
                    {[account.institution, account.maskedNumber].filter(Boolean).join(' · ') ||
                      'Manual account'}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <p className="font-semibold tabular-nums">
                    {formatMoney(account.currentBalance, account.currency)}
                  </p>
                  <p className="text-xs text-slate-400">{account.currency}</p>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          title="No accounts yet"
          description="Add a checking, savings, card, or cash account — or upload a statement and we'll create one for you."
          action={
            <Button onClick={() => setOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden /> Add account
            </Button>
          }
        />
      )}

      <AccountDialog open={open} onClose={() => setOpen(false)} />
    </div>
  )
}

function AccountDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const create = useCreateAccount()
  const form = useZodForm(accountSchema, {
    defaultValues: { name: '', type: 'Checking', institution: '', currency: 'MXN', number: '' },
  })

  function submit(values: AccountFormValues) {
    create.mutate(
      {
        name: values.name,
        type: values.type,
        institution: values.institution || null,
        currency: values.currency || 'MXN',
        number: values.number || null,
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
    <Dialog open={open} onClose={onClose} title="Add account" description="Create a new household account.">
      <form className="space-y-3" onSubmit={form.handleSubmit(submit)}>
        <FormField label="Name" error={form.formState.errors.name?.message}>
          {(id) => <Input id={id} placeholder="BBVA Débito" {...form.register('name')} />}
        </FormField>

        <FormField label="Type" error={form.formState.errors.type?.message}>
          {(id) => (
            <Select id={id} {...form.register('type')}>
              {ACCOUNT_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </Select>
          )}
        </FormField>

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Institution" error={form.formState.errors.institution?.message}>
            {(id) => <Input id={id} placeholder="BBVA" {...form.register('institution')} />}
          </FormField>
          <FormField label="Currency" error={form.formState.errors.currency?.message}>
            {(id) => <Input id={id} placeholder="MXN" maxLength={3} {...form.register('currency')} />}
          </FormField>
        </div>

        <FormField
          label="Account number"
          hint="Optional — only the last 4 digits are stored (masked)."
          error={form.formState.errors.number?.message}
        >
          {(id) => <Input id={id} inputMode="numeric" placeholder="1234567812345678" {...form.register('number')} />}
        </FormField>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Saving…' : 'Add account'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
