/**
 * Integrations page (epic 9, surface 4) — connect outside channels to the-ledger. v1.1 ships the
 * WhatsApp channel: opt a phone number in (GET/POST /connectors/whatsapp/opt-in), and revoke it
 * (DELETE). Once connected, you can capture by texting your ledger and receive alerts on WhatsApp.
 *
 * Mobile-first: a stacked card with a clear status, a how-to-connect explainer, and an opt-in form.
 */
import { type ReactNode } from 'react'
import { MessageCircle, Plus, Trash2, ShieldCheck } from 'lucide-react'
import { PageHeader } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { ErrorState } from '../components/ui/states'
import { Skeleton } from '../components/ui/skeleton'
import { FormField, useZodForm } from '../components/ui/form'
import {
  useAccounts,
  useRevokeWhatsApp,
  useWhatsAppOptIn,
  useWhatsAppOptIns,
} from '../api/hooks'
import { whatsappOptInSchema, type WhatsAppOptInValues } from '../features/capture/schema'
import type { WhatsAppOptInDto } from '../api/types'

export function Integrations() {
  const optIns = useWhatsAppOptIns()
  const connected = (optIns.data ?? []).filter((o) => o.optedIn)

  return (
    <div className="space-y-5">
      <PageHeader
        title="Integrations"
        subtitle="Connect channels so you can capture and get alerts where you already are."
      />

      <section className="overflow-hidden rounded-3xl border border-slate-200 dark:border-slate-800">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-emerald-50/40 p-4 dark:border-slate-800 dark:bg-emerald-950/20">
          <div className="flex items-center gap-3">
            <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-emerald-500 text-white shadow-sm">
              <MessageCircle className="h-6 w-6" aria-hidden />
            </span>
            <div>
              <h2 className="text-base font-semibold">WhatsApp</h2>
              <p className="text-sm text-slate-500">Capture by text, get bill & anomaly alerts.</p>
            </div>
          </div>
          {optIns.isLoading ? (
            <Skeleton className="h-6 w-24 rounded-full" />
          ) : connected.length > 0 ? (
            <Badge tone="green">
              {connected.length} number{connected.length === 1 ? '' : 's'} connected
            </Badge>
          ) : (
            <Badge tone="neutral">Not connected</Badge>
          )}
        </header>

        <div className="space-y-5 p-4">
          <ol className="space-y-1.5 text-sm text-slate-600 dark:text-slate-300">
            <Step n={1}>Opt your WhatsApp number in below — this records your consent.</Step>
            <Step n={2}>Send your ledger a message like “gasté 200 en el Oxxo ayer”.</Step>
            <Step n={3}>It lands in your Review queue to confirm — never auto-posted.</Step>
          </ol>

          {optIns.isError ? (
            <ErrorState message="Could not load your WhatsApp connections." onRetry={() => optIns.refetch()} />
          ) : (
            <>
              <ConnectedList items={connected} loading={optIns.isLoading} />
              <OptInForm />
            </>
          )}

          <p className="flex items-start gap-2 rounded-xl bg-slate-50 px-3 py-2 text-xs text-slate-500 dark:bg-slate-800/50">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-500" aria-hidden />
            Only opted-in numbers can reach your household data. Disconnecting removes the mapping and
            the consent immediately.
          </p>
        </div>
      </section>
    </div>
  )
}

function Step({ n, children }: { n: number; children: ReactNode }) {
  return (
    <li className="flex items-start gap-2">
      <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-violet-100 text-xs font-semibold text-violet-700 dark:bg-violet-950 dark:text-violet-300">
        {n}
      </span>
      <span>{children}</span>
    </li>
  )
}

function ConnectedList({ items, loading }: { items: WhatsAppOptInDto[]; loading: boolean }) {
  const revoke = useRevokeWhatsApp()
  if (loading) {
    return <Skeleton className="h-12 w-full rounded-2xl" />
  }
  if (items.length === 0) {
    return null
  }
  return (
    <ul className="space-y-2">
      {items.map((o) => (
        <li
          key={o.id}
          className="flex items-center justify-between gap-3 rounded-2xl border border-slate-200 p-3 dark:border-slate-800"
        >
          <div className="flex min-w-0 items-center gap-3">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-emerald-50 text-emerald-600 dark:bg-emerald-950 dark:text-emerald-300">
              <MessageCircle className="h-4 w-4" aria-hidden />
            </span>
            <span className="truncate font-medium tabular-nums">+{o.phoneNumber}</span>
          </div>
          <Button
            variant="ghost"
            size="sm"
            aria-label={`Disconnect ${o.phoneNumber}`}
            onClick={() => revoke.mutate(o.id)}
            disabled={revoke.isPending}
            className="text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40"
          >
            <Trash2 className="h-4 w-4" aria-hidden /> Disconnect
          </Button>
        </li>
      ))}
    </ul>
  )
}

function OptInForm() {
  const accounts = useAccounts()
  const optIn = useWhatsAppOptIn()
  const form = useZodForm(whatsappOptInSchema, {
    defaultValues: { phoneNumber: '', defaultAccountId: '' },
  })

  function submit(values: WhatsAppOptInValues) {
    optIn.mutate(
      {
        phoneNumber: values.phoneNumber.replace(/^\+/, ''),
        defaultAccountId: values.defaultAccountId || null,
      },
      { onSuccess: () => form.reset({ phoneNumber: '', defaultAccountId: '' }) },
    )
  }

  return (
    <form
      onSubmit={form.handleSubmit(submit)}
      className="space-y-3 rounded-2xl border border-dashed border-slate-300 p-4 dark:border-slate-700"
    >
      <p className="text-sm font-medium">Connect a number</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField
          label="WhatsApp number"
          error={form.formState.errors.phoneNumber?.message}
          hint="International format, e.g. 5215512345678"
        >
          {(id) => (
            <Input
              id={id}
              type="tel"
              inputMode="tel"
              placeholder="5215512345678"
              {...form.register('phoneNumber')}
            />
          )}
        </FormField>
        <FormField label="Default account (optional)" error={form.formState.errors.defaultAccountId?.message}>
          {(id) => (
            <Select id={id} {...form.register('defaultAccountId')}>
              <option value="">No default</option>
              {(accounts.data ?? []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </Select>
          )}
        </FormField>
      </div>
      <div className="flex justify-end">
        <Button type="submit" disabled={optIn.isPending}>
          <Plus className="h-4 w-4" aria-hidden />
          {optIn.isPending ? 'Connecting…' : 'Connect WhatsApp'}
        </Button>
      </div>
    </form>
  )
}
