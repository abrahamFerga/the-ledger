/**
 * QuickAddBar (epic 9, surface 1) — the celebrated, AI-first capture entry point, persistent on
 * every page. Type a phrase ("gasté 200 en el Oxxo ayer"), and the AI parses it into a draft shown
 * in a confirm sheet; nothing posts to the ledger until you confirm (ADR-0011).
 *
 * Layout: a prominent pill bar. On mobile it floats just above the thumb-reachable bottom nav so it's
 * one-handed; on desktop it sits inline in the page chrome. A "Scan" affordance links to receipt
 * capture so the two primary capture actions live together.
 */
import { useState, type FormEvent } from 'react'
import { Sparkles, ArrowUp, Loader2 } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { Camera } from 'lucide-react'
import { useQuickAdd } from '../../api/hooks'
import type { TransactionDraft } from '../../api/types'
import { ConfirmDraftSheet } from './ConfirmDraftSheet'

export function QuickAddBar() {
  const [text, setText] = useState('')
  const [draft, setDraft] = useState<TransactionDraft | null>(null)
  const [confirmedText, setConfirmedText] = useState('')
  const [open, setOpen] = useState(false)
  const quickAdd = useQuickAdd()
  const navigate = useNavigate()

  function onSubmit(e: FormEvent) {
    e.preventDefault()
    const phrase = text.trim()
    if (!phrase || quickAdd.isPending) {
      return
    }
    quickAdd.mutate(
      { text: phrase },
      {
        onSuccess: (parsed) => {
          setDraft(parsed)
          setConfirmedText(phrase)
          setOpen(true)
        },
      },
    )
  }

  return (
    <>
      <div className="pointer-events-none fixed inset-x-0 bottom-16 z-30 px-3 md:sticky md:bottom-auto md:top-16 md:px-0 md:pt-3">
        <div className="pointer-events-auto mx-auto w-full max-w-6xl">
          <form
            onSubmit={onSubmit}
            className="flex items-center gap-2 rounded-2xl border border-violet-200 bg-white/95 p-1.5 pl-3 shadow-lg shadow-violet-500/5 ring-1 ring-violet-500/10 backdrop-blur transition-shadow focus-within:ring-2 focus-within:ring-violet-500/40 dark:border-violet-900 dark:bg-slate-900/95"
          >
            <Sparkles className="h-5 w-5 shrink-0 text-violet-500" aria-hidden />
            <input
              type="text"
              value={text}
              onChange={(e) => setText(e.target.value)}
              aria-label="Quick add a transaction in plain language"
              placeholder="Add an expense… e.g. “gasté 200 en el Oxxo ayer”"
              enterKeyHint="send"
              className="min-w-0 flex-1 bg-transparent py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none dark:text-slate-100"
            />
            <button
              type="button"
              onClick={() => navigate('/capture')}
              aria-label="Scan a receipt instead"
              title="Scan a receipt"
              className="hidden h-11 items-center gap-1.5 rounded-xl px-3 text-sm font-medium text-violet-700 hover:bg-violet-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500 sm:inline-flex dark:text-violet-300 dark:hover:bg-violet-950"
            >
              <Camera className="h-4 w-4" aria-hidden /> Scan
            </button>
            <button
              type="submit"
              aria-label="Parse and confirm"
              disabled={!text.trim() || quickAdd.isPending}
              className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-violet-600 text-white transition-colors hover:bg-violet-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500 focus-visible:ring-offset-1 disabled:cursor-not-allowed disabled:bg-violet-300"
            >
              {quickAdd.isPending ? (
                <Loader2 className="h-5 w-5 animate-spin" aria-hidden />
              ) : (
                <ArrowUp className="h-5 w-5" aria-hidden />
              )}
            </button>
          </form>
        </div>
      </div>

      <ConfirmDraftSheet
        open={open}
        draft={draft}
        sourceText={confirmedText}
        onClose={() => setOpen(false)}
        onConfirmed={() => {
          setText('')
          setDraft(null)
        }}
      />
    </>
  )
}
