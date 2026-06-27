import { useMemo, useRef, useState } from 'react'
import { CheckCircle2, FileUp } from 'lucide-react'
import {
  useAccounts,
  useConfirmStatement,
  useImportCsv,
  useReviewQueue,
  useUploadPdf,
} from '../api/hooks'
import type { TransactionDto } from '../api/types'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { formatDate, formatMoney } from '../lib/utils'

export function Statements() {
  const accounts = useAccounts()
  const [accountId, setAccountId] = useState('')
  const importCsv = useImportCsv()
  const uploadPdf = useUploadPdf()
  const fileRef = useRef<HTMLInputElement>(null)

  async function onFile(file: File | undefined) {
    if (!file || !accountId) {
      return
    }
    const isCsv = file.name.toLowerCase().endsWith('.csv') || file.type === 'text/csv'
    if (isCsv) {
      const content = await file.text()
      importCsv.mutate({ accountId, fileName: file.name, content })
    } else {
      uploadPdf.mutate({ accountId, file })
    }
    if (fileRef.current) {
      fileRef.current.value = ''
    }
  }

  const busy = importCsv.isPending || uploadPdf.isPending
  const needsAccount = !accountId

  return (
    <div className="space-y-5">
      <PageHeader
        title="Upload a statement"
        subtitle="Import a PDF or CSV; rows land in the review queue until you confirm them."
      />

      <div className="space-y-2">
        <Select
          aria-label="Account for this statement"
          value={accountId}
          onChange={(e) => setAccountId(e.target.value)}
        >
          <option value="">Choose the account…</option>
          {(accounts.data ?? []).map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </Select>

        <label
          className={`flex h-44 flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed text-slate-500 ${
            needsAccount
              ? 'cursor-not-allowed border-slate-200 opacity-60 dark:border-slate-800'
              : 'cursor-pointer border-slate-300 hover:border-violet-400 dark:border-slate-700'
          }`}
        >
          <FileUp className="h-8 w-8" aria-hidden />
          <span className="text-sm font-medium">
            {busy ? 'Uploading…' : 'Tap to choose a PDF or CSV'}
          </span>
          <span className="text-xs text-slate-400">or take a photo of a statement</span>
          <input
            ref={fileRef}
            type="file"
            accept=".pdf,.csv,image/*"
            capture="environment"
            className="hidden"
            disabled={needsAccount || busy}
            onChange={(e) => onFile(e.target.files?.[0])}
          />
        </label>
        {needsAccount ? (
          <p className="text-xs text-amber-600">Choose an account before uploading.</p>
        ) : null}
        <p className="text-xs text-slate-400">
          Supported: BBVA, Santander, Banorte, and digital banks (Nu / Hey / Klar). Card numbers are
          masked on import; nothing leaves your household.
        </p>
      </div>

      <ReviewQueue />
    </div>
  )
}

/** The review-and-confirm queue: staged (unconfirmed) transactions grouped by statement. */
function ReviewQueue() {
  const queue = useReviewQueue()
  const confirm = useConfirmStatement()

  const groups = useMemo(() => {
    const byStatement = new Map<string, TransactionDto[]>()
    const manual: TransactionDto[] = []
    for (const tx of queue.data ?? []) {
      if (tx.statementId) {
        const list = byStatement.get(tx.statementId) ?? []
        list.push(tx)
        byStatement.set(tx.statementId, list)
      } else {
        manual.push(tx)
      }
    }
    return { byStatement, manual }
  }, [queue.data])

  return (
    <section className="space-y-3">
      <h2 className="text-sm font-semibold text-slate-500">Review &amp; confirm</h2>

      {queue.isLoading ? (
        <LoadingState label="Loading review queue" />
      ) : queue.isError ? (
        <ErrorState message="Could not load the review queue." onRetry={() => queue.refetch()} />
      ) : (queue.data?.length ?? 0) === 0 ? (
        <EmptyState
          title="Nothing to review"
          description="Imported transactions appear here for you to confirm or reject before they hit the ledger."
        />
      ) : (
        <div className="space-y-4">
          {[...groups.byStatement.entries()].map(([statementId, rows]) => (
            <Card key={statementId}>
              <div className="mb-2 flex items-center justify-between">
                <div className="flex items-center gap-2 text-sm">
                  <Badge tone="amber">Staged</Badge>
                  <span className="text-slate-500">{rows.length} transaction(s)</span>
                </div>
                <Button
                  size="sm"
                  onClick={() => confirm.mutate(statementId)}
                  disabled={confirm.isPending}
                >
                  <CheckCircle2 className="h-4 w-4" aria-hidden /> Confirm all
                </Button>
              </div>
              <StagedRows rows={rows} />
            </Card>
          ))}

          {groups.manual.length > 0 ? (
            <Card>
              <div className="mb-2 flex items-center gap-2 text-sm">
                <Badge tone="violet">Manual</Badge>
                <span className="text-slate-500">{groups.manual.length} unconfirmed</span>
              </div>
              <StagedRows rows={groups.manual} />
            </Card>
          ) : null}
        </div>
      )}
    </section>
  )
}

function StagedRows({ rows }: { rows: TransactionDto[] }) {
  return (
    <ul className="divide-y divide-slate-100 dark:divide-slate-800">
      {rows.map((tx) => (
        <li key={tx.id} className="flex items-center justify-between gap-3 py-1.5 text-sm">
          <div className="min-w-0">
            <p className="truncate">{tx.description}</p>
            <p className="text-xs text-slate-400">{formatDate(tx.date)}</p>
          </div>
          <span
            className={`shrink-0 tabular-nums ${
              tx.direction === 'Credit' ? 'text-emerald-600' : 'text-slate-900 dark:text-slate-100'
            }`}
          >
            {tx.direction === 'Credit' ? '+' : '−'}
            {formatMoney(tx.amount, tx.currency)}
          </span>
        </li>
      ))}
    </ul>
  )
}
