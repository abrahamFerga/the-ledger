/**
 * Capture page (epic 9, surface 2) — receipt/ticket photo capture. Snap a ticket with the device
 * camera (or pick a file) → POST /receipts (multipart) → the worker OCRs it and stages a transaction
 * in the review queue. Mobile-first and one-handed: a big camera tile is the primary action.
 *
 * The quick-add bar (surface 1) lives in the app shell, so this page focuses on the photo path and
 * the list of scanned receipts with their OCR status.
 */
import { useRef, useState } from 'react'
import { Camera, ImagePlus, ScanLine, ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../components/ui/page'
import { Select } from '../components/ui/select'
import { Badge } from '../components/ui/badge'
import { EmptyState, ErrorState } from '../components/ui/states'
import { SkeletonCards } from '../components/ui/skeleton'
import { useAccounts, useReceipts, useUploadReceipt } from '../api/hooks'
import { formatDate, formatMoney } from '../lib/utils'
import type { ReceiptDto } from '../api/types'

function statusTone(status: string): 'amber' | 'green' | 'rose' | 'neutral' {
  const s = status.toLowerCase()
  if (s.includes('fail') || s.includes('error')) {
    return 'rose'
  }
  if (s.includes('extract') || s.includes('done') || s.includes('complete') || s.includes('staged')) {
    return 'green'
  }
  if (s.includes('pend') || s.includes('queue') || s.includes('process') || s.includes('scan')) {
    return 'amber'
  }
  return 'neutral'
}

export function Capture() {
  const accounts = useAccounts()
  const receipts = useReceipts()
  const upload = useUploadReceipt()
  const [accountId, setAccountId] = useState('')
  const cameraRef = useRef<HTMLInputElement>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const effectiveAccountId = accountId || accounts.data?.[0]?.id || ''

  function onPicked(file: File | undefined) {
    if (!file || !effectiveAccountId) {
      return
    }
    upload.mutate({ accountId: effectiveAccountId, file })
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Scan a receipt"
        subtitle="Snap a ticket — we read the merchant, date and total, then stage it for you to confirm."
      />

      {/* Account picker (where the confirmed transaction lands). */}
      <div className="flex flex-wrap items-center gap-2">
        <label className="text-sm text-slate-600 dark:text-slate-300" htmlFor="receipt-account">
          Account
        </label>
        <Select
          id="receipt-account"
          aria-label="Account for the scanned receipt"
          value={effectiveAccountId}
          onChange={(e) => setAccountId(e.target.value)}
          className="w-auto min-w-[12rem]"
        >
          {(accounts.data ?? []).map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </Select>
      </div>

      {/* Hidden inputs: camera (rear-facing) + plain file picker. */}
      <input
        ref={cameraRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="sr-only"
        aria-hidden
        tabIndex={-1}
        onChange={(e) => {
          onPicked(e.target.files?.[0])
          e.target.value = ''
        }}
      />
      <input
        ref={fileRef}
        type="file"
        accept="image/*"
        className="sr-only"
        aria-hidden
        tabIndex={-1}
        onChange={(e) => {
          onPicked(e.target.files?.[0])
          e.target.value = ''
        }}
      />

      {/* Primary action: a big, tappable camera tile (≥44px targets, celebrated AI action). */}
      <div className="grid gap-3 sm:grid-cols-2">
        <button
          type="button"
          onClick={() => cameraRef.current?.click()}
          disabled={!effectiveAccountId || upload.isPending}
          className="group flex flex-col items-center justify-center gap-2 rounded-3xl border-2 border-dashed border-violet-300 bg-violet-50/60 px-6 py-10 text-center transition-colors hover:border-violet-400 hover:bg-violet-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500 disabled:cursor-not-allowed disabled:opacity-60 dark:border-violet-800 dark:bg-violet-950/40 dark:hover:bg-violet-950/70"
        >
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-violet-600 text-white shadow-lg shadow-violet-600/20 transition-transform group-hover:scale-105">
            <Camera className="h-7 w-7" aria-hidden />
          </span>
          <span className="text-base font-semibold text-violet-900 dark:text-violet-100">
            {upload.isPending ? 'Uploading…' : 'Take a photo'}
          </span>
          <span className="text-xs text-violet-700/80 dark:text-violet-300/80">
            Uses your camera on a phone
          </span>
        </button>

        <button
          type="button"
          onClick={() => fileRef.current?.click()}
          disabled={!effectiveAccountId || upload.isPending}
          className="flex flex-col items-center justify-center gap-2 rounded-3xl border border-slate-200 bg-white px-6 py-10 text-center transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-800 dark:bg-slate-900 dark:hover:bg-slate-800/60"
        >
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <ImagePlus className="h-7 w-7" aria-hidden />
          </span>
          <span className="text-base font-semibold">Upload an image</span>
          <span className="text-xs text-slate-500">Pick a photo from this device</span>
        </button>
      </div>

      {!effectiveAccountId && !accounts.isLoading ? (
        <p className="text-sm text-amber-700 dark:text-amber-300">
          Add an account first so we know where to file the receipt.
        </p>
      ) : null}

      {/* Scanned receipts list. */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-700 dark:text-slate-200">
            <ScanLine className="h-4 w-4 text-violet-500" aria-hidden /> Recent scans
          </h2>
          <Link
            to="/review"
            className="inline-flex items-center gap-1 text-sm font-medium text-violet-700 hover:underline dark:text-violet-300"
          >
            Review queue <ArrowRight className="h-3.5 w-3.5" aria-hidden />
          </Link>
        </div>

        {receipts.isLoading ? (
          <SkeletonCards count={2} />
        ) : receipts.isError ? (
          <ErrorState message="Could not load your scans." onRetry={() => receipts.refetch()} />
        ) : (receipts.data ?? []).length === 0 ? (
          <EmptyState
            title="No scans yet"
            description="Snap your first receipt — it lands here while we read it."
          />
        ) : (
          <ul className="space-y-2">
            {(receipts.data ?? []).map((r) => (
              <ReceiptRow key={r.id} receipt={r} tone={statusTone(r.status)} />
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}

function ReceiptRow({
  receipt,
  tone,
}: {
  receipt: ReceiptDto
  tone: 'amber' | 'green' | 'rose' | 'neutral'
}) {
  const pending = tone === 'amber'
  return (
    <li className="flex items-center justify-between gap-3 rounded-2xl border border-slate-200 p-3.5 dark:border-slate-800">
      <div className="flex min-w-0 items-center gap-3">
        <span
          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${
            pending
              ? 'bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-300'
              : 'bg-slate-100 text-slate-500 dark:bg-slate-800'
          }`}
        >
          <ScanLine className={`h-5 w-5 ${pending ? 'animate-pulse' : ''}`} aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="truncate font-medium">
            {receipt.merchant ?? (pending ? 'Reading receipt…' : 'Receipt')}
          </p>
          <p className="text-xs text-slate-500">
            {receipt.transactionDate ? formatDate(receipt.transactionDate) : 'Date pending'}
            {receipt.needsReview ? ' · needs a check' : ''}
          </p>
        </div>
      </div>
      <div className="flex shrink-0 flex-col items-end gap-1">
        {receipt.total != null ? (
          <span className="font-medium tabular-nums">
            {formatMoney(receipt.total, receipt.currency)}
          </span>
        ) : null}
        <Badge tone={tone}>{receipt.status}</Badge>
      </div>
    </li>
  )
}
