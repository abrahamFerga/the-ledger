/**
 * The confirm sheet behind the quick-add bar (epic 9, surface 1). It receives the AI-parsed
 * `TransactionDraft`, shows the parse (amount/date/direction/merchant) with its confidence, and lets
 * the user correct any field before confirming. Confirming creates the transaction through the
 * existing manual-create path — the draft is NEVER auto-persisted (ADR-0011).
 */
import { useEffect } from 'react'
import { Sparkles, Check } from 'lucide-react'
import { Sheet } from '../../components/ui/sheet'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Select } from '../../components/ui/select'
import { ConfidenceMeter } from '../../components/ui/confidence-meter'
import { FormField, useZodForm } from '../../components/ui/form'
import { useAccounts, useAddManualTransaction, useCategories } from '../../api/hooks'
import { TRANSACTION_DIRECTIONS, type TransactionDraft } from '../../api/types'
import { confirmDraftSchema, type ConfirmDraftValues } from './schema'

export function ConfirmDraftSheet({
  open,
  draft,
  sourceText,
  presetAccountId,
  onClose,
  onConfirmed,
}: {
  open: boolean
  draft: TransactionDraft | null
  /** The original phrase, echoed so the user sees what the AI read. */
  sourceText?: string
  presetAccountId?: string
  onClose: () => void
  onConfirmed: () => void
}) {
  const accounts = useAccounts()
  const categories = useCategories()
  const add = useAddManualTransaction()

  const form = useZodForm(confirmDraftSchema, {
    defaultValues: {
      accountId: presetAccountId ?? '',
      date: draft?.date ?? '',
      description: draft?.merchant ?? '',
      amount: draft?.amount,
      direction: draft?.direction ?? 'Debit',
    },
  })

  const { reset } = form
  // Re-seed the form whenever a fresh draft arrives. Default to the first account if none preset.
  useEffect(() => {
    if (!draft) {
      return
    }
    const firstAccount = presetAccountId || accounts.data?.[0]?.id || ''
    reset({
      accountId: firstAccount,
      date: draft.date,
      description: draft.merchant ?? '',
      amount: draft.amount,
      direction: draft.direction,
    })
  }, [draft, presetAccountId, accounts.data, reset])

  const proposedCategory = draft?.proposedCategoryId
    ? categories.data?.find((c) => c.id === draft.proposedCategoryId)
    : undefined

  function submit(values: ConfirmDraftValues) {
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
          onConfirmed()
          onClose()
        },
      },
    )
  }

  return (
    <Sheet
      open={open}
      onClose={onClose}
      title="Confirm transaction"
      description="Review what we read, edit anything, then save it to your ledger."
      icon={<Sparkles className="h-5 w-5" aria-hidden />}
      footer={
        <div className="flex items-center gap-2">
          <Button variant="secondary" className="flex-1" onClick={onClose}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="confirm-draft-form"
            className="flex-1"
            disabled={add.isPending}
          >
            {add.isPending ? (
              'Saving…'
            ) : (
              <>
                <Check className="h-4 w-4" aria-hidden /> Add to ledger
              </>
            )}
          </Button>
        </div>
      }
    >
      <form id="confirm-draft-form" className="space-y-4 pb-2" onSubmit={form.handleSubmit(submit)}>
        {sourceText ? (
          <p className="rounded-xl bg-violet-50 px-3 py-2 text-sm text-violet-800 dark:bg-violet-950/60 dark:text-violet-200">
            <span className="font-medium">You said:</span> “{sourceText}”
          </p>
        ) : null}

        {draft ? <ConfidenceMeter confidence={draft.confidence} /> : null}

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

        <FormField label="Merchant / description" error={form.formState.errors.description?.message}>
          {(id) => <Input id={id} placeholder="OXXO, Nómina, …" {...form.register('description')} />}
        </FormField>

        <div className="grid grid-cols-2 gap-3">
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
          <FormField label="Date" error={form.formState.errors.date?.message}>
            {(id) => <Input id={id} type="date" {...form.register('date')} />}
          </FormField>
        </div>

        <FormField label="Direction" error={form.formState.errors.direction?.message}>
          {(id) => (
            <Select id={id} {...form.register('direction')}>
              {TRANSACTION_DIRECTIONS.map((d) => (
                <option key={d} value={d}>
                  {d === 'Debit' ? 'Expense (money out)' : 'Income (money in)'}
                </option>
              ))}
            </Select>
          )}
        </FormField>

        {proposedCategory ? (
          <p className="text-xs text-slate-500">
            Suggested category: <span className="font-medium">{proposedCategory.name}</span>. You can
            set it on the ledger after saving.
          </p>
        ) : null}
      </form>
    </Sheet>
  )
}
