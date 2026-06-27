/**
 * Review & confirm queue (epic 9, surface 3) — every staged, unconfirmed transaction from every
 * capture source (CSV/PDF statements, receipt OCR, WhatsApp, quick-add) in one place. Nothing posts
 * to the ledger until it's confirmed here.
 *
 * Inline edit (description + category) flows through the existing PATCH /transactions/{id}.
 * Confirmation is statement-scoped on the backend (POST /statements/{id}/confirm), so items that
 * belong to a statement are grouped and confirmed as a batch with an optimistic removal. Statement-
 * less items (receipt/quick-add/WhatsApp captures) are editable here and confirm automatically when
 * their source completes — there is no per-item confirm endpoint on the API yet, so we say so plainly
 * rather than wiring a dead button.
 *
 * Mobile-first: cards stacked vertically (never a cramped wide table); ≥44px tap targets.
 */
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Check, Pencil, Inbox, Sparkles, FileText, X } from 'lucide-react'
import { PageHeader } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Input } from '../components/ui/input'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { ErrorState } from '../components/ui/states'
import { SkeletonCards } from '../components/ui/skeleton'
import {
  useCategories,
  useConfirmReviewBatch,
  useReviewQueue,
  useUpdateTransaction,
} from '../api/hooks'
import { formatDate, formatMoney } from '../lib/utils'
import type { CategoryDto, TransactionDto } from '../api/types'

export function Review() {
  const review = useReviewQueue()
  const categories = useCategories()
  const confirmBatch = useConfirmReviewBatch()

  const groups = useMemo(() => groupByStatement(review.data ?? []), [review.data])

  return (
    <div className="space-y-5">
      <PageHeader
        title="Review & confirm"
        subtitle="Everything you captured, waiting for your OK. Nothing hits your ledger until you confirm it."
      />

      {review.isLoading ? (
        <SkeletonCards count={3} />
      ) : review.isError ? (
        <ErrorState message="Could not load the review queue." onRetry={() => review.refetch()} />
      ) : (review.data ?? []).length === 0 ? (
        <EmptyReview />
      ) : (
        <div className="space-y-6">
          {groups.statements.map((group) => (
            <StatementGroup
              key={group.statementId}
              group={group}
              categories={categories.data ?? []}
              confirming={confirmBatch.isPending}
              onConfirm={() => confirmBatch.mutate(group.statementId)}
            />
          ))}

          {groups.captured.length > 0 ? (
            <CapturedGroup items={groups.captured} categories={categories.data ?? []} />
          ) : null}
        </div>
      )}
    </div>
  )
}

interface StatementBucket {
  statementId: string
  items: TransactionDto[]
}

function groupByStatement(items: TransactionDto[]): {
  statements: StatementBucket[]
  captured: TransactionDto[]
} {
  const byStatement = new Map<string, TransactionDto[]>()
  const captured: TransactionDto[] = []
  for (const item of items) {
    if (item.statementId) {
      const list = byStatement.get(item.statementId) ?? []
      list.push(item)
      byStatement.set(item.statementId, list)
    } else {
      captured.push(item)
    }
  }
  return {
    statements: Array.from(byStatement, ([statementId, group]) => ({ statementId, items: group })),
    captured,
  }
}

function EmptyReview() {
  return (
    <div className="flex flex-col items-center gap-3 rounded-3xl border border-dashed border-slate-300 px-6 py-14 text-center dark:border-slate-700">
      <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-600 dark:bg-emerald-950 dark:text-emerald-300">
        <Inbox className="h-6 w-6" aria-hidden />
      </span>
      <p className="text-lg font-semibold">You're all caught up</p>
      <p className="max-w-xs text-sm text-slate-500">
        Nothing waiting to confirm. Capture something with the quick-add bar or by scanning a receipt.
      </p>
      <Link
        to="/capture"
        className="mt-1 inline-flex items-center gap-1.5 rounded-lg bg-violet-600 px-3 py-2 text-sm font-medium text-white hover:bg-violet-700"
      >
        <Sparkles className="h-4 w-4" aria-hidden /> Capture something
      </Link>
    </div>
  )
}

function StatementGroup({
  group,
  categories,
  confirming,
  onConfirm,
}: {
  group: StatementBucket
  categories: CategoryDto[]
  confirming: boolean
  onConfirm: () => void
}) {
  const total = group.items.length
  return (
    <section className="rounded-3xl border border-slate-200 dark:border-slate-800">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-4 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <FileText className="h-4 w-4" aria-hidden />
          </span>
          <div>
            <p className="text-sm font-semibold">Imported statement</p>
            <p className="text-xs text-slate-500">
              {total} transaction{total === 1 ? '' : 's'} staged
            </p>
          </div>
        </div>
        <Button onClick={onConfirm} disabled={confirming}>
          <Check className="h-4 w-4" aria-hidden />
          {confirming ? 'Confirming…' : `Confirm all (${total})`}
        </Button>
      </header>
      <ul className="divide-y divide-slate-100 dark:divide-slate-800">
        {group.items.map((item) => (
          <StagedRow key={item.id} item={item} categories={categories} />
        ))}
      </ul>
    </section>
  )
}

function CapturedGroup({
  items,
  categories,
}: {
  items: TransactionDto[]
  categories: CategoryDto[]
}) {
  return (
    <section className="rounded-3xl border border-violet-200 dark:border-violet-900">
      <header className="flex items-center gap-2 border-b border-violet-100 p-4 dark:border-violet-900/60">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-violet-100 text-violet-700 dark:bg-violet-950 dark:text-violet-300">
          <Sparkles className="h-4 w-4" aria-hidden />
        </span>
        <div>
          <p className="text-sm font-semibold">From AI capture</p>
          <p className="text-xs text-slate-500">
            Receipts, WhatsApp and quick-add. Edit anything here; they confirm as their scan finishes.
          </p>
        </div>
      </header>
      <ul className="divide-y divide-violet-100 dark:divide-violet-900/60">
        {items.map((item) => (
          <StagedRow key={item.id} item={item} categories={categories} />
        ))}
      </ul>
    </section>
  )
}

/** One staged transaction card: amount, date, inline-editable description + category. */
function StagedRow({ item, categories }: { item: TransactionDto; categories: CategoryDto[] }) {
  const update = useUpdateTransaction()
  const [editing, setEditing] = useState(false)
  const [description, setDescription] = useState(item.description)
  const isCredit = item.direction === 'Credit'

  function save() {
    const next = description.trim()
    if (next && next !== item.description) {
      update.mutate({ id: item.id, body: { description: next } })
    }
    setEditing(false)
  }

  return (
    <li className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          {editing ? (
            <div className="flex w-full items-center gap-1">
              <Input
                aria-label="Edit description"
                value={description}
                autoFocus
                onChange={(e) => setDescription(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    save()
                  } else if (e.key === 'Escape') {
                    setDescription(item.description)
                    setEditing(false)
                  }
                }}
                className="h-9"
              />
              <Button size="icon" aria-label="Save description" onClick={save}>
                <Check className="h-4 w-4" aria-hidden />
              </Button>
              <Button
                variant="secondary"
                size="icon"
                aria-label="Cancel edit"
                onClick={() => {
                  setDescription(item.description)
                  setEditing(false)
                }}
              >
                <X className="h-4 w-4" aria-hidden />
              </Button>
            </div>
          ) : (
            <button
              type="button"
              className="group inline-flex min-w-0 items-center gap-1.5 text-left font-medium hover:text-violet-700"
              onClick={() => {
                setDescription(item.description)
                setEditing(true)
              }}
            >
              <span className="truncate">{item.description}</span>
              <Pencil className="h-3.5 w-3.5 shrink-0 opacity-0 group-hover:opacity-60" aria-hidden />
            </button>
          )}
        </div>
        {!editing ? (
          <p className="mt-0.5 text-xs text-slate-500">{formatDate(item.date)}</p>
        ) : null}
      </div>

      <div className="flex items-center justify-between gap-3 sm:justify-end">
        <Select
          aria-label={`Category for ${item.description}`}
          defaultValue=""
          onChange={(e) => update.mutate({ id: item.id, body: { categoryId: e.target.value || null } })}
          className="h-9 w-auto min-w-[8.5rem] text-xs"
        >
          <option value="">Categorize…</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        <span
          className={`shrink-0 font-semibold tabular-nums ${
            isCredit ? 'text-emerald-600' : 'text-slate-900 dark:text-slate-100'
          }`}
        >
          {isCredit ? '+' : '−'}
          {formatMoney(item.amount, item.currency)}
        </span>
        <Badge tone="amber">Staged</Badge>
      </div>
    </li>
  )
}
