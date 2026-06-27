/**
 * Sheet — an owned, dependency-free overlay tuned for the capture confirm flows (epic 9). On mobile
 * it slides up as a thumb-reachable bottom sheet with a grab handle; on `sm+` it becomes a centered
 * card. Focus moves into the panel on open, is trapped while open, and restored to the trigger on
 * close; Escape and a backdrop tap dismiss it. Body scrolls independently so a tall confirm form
 * never pushes the footer off-screen.
 *
 * Distinct from `Dialog` (a compact modal): `Sheet` gives a sticky header + scrollable body +
 * sticky footer, which the quick-add / receipt confirm UX needs on a small screen.
 */
import { useEffect, useRef, type ReactNode } from 'react'
import { X } from 'lucide-react'
import { useCallbackRef } from '../../lib/useCallbackRef'
import { Button } from './button'

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea, input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'

interface SheetProps {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  /** Optional leading icon badge in the header (e.g. the Sparkles/Camera capture mark). */
  icon?: ReactNode
  /** Sticky footer (typically the confirm/cancel actions). */
  footer?: ReactNode
  children: ReactNode
}

export function Sheet({ open, onClose, title, description, icon, footer, children }: SheetProps) {
  const panelRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useCallbackRef(onClose)

  useEffect(() => {
    if (!open) {
      return
    }
    const previouslyFocused = document.activeElement as HTMLElement | null
    const panel = panelRef.current
    panel?.querySelector<HTMLElement>(FOCUSABLE)?.focus()

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        e.stopPropagation()
        onCloseRef()
        return
      }
      if (e.key !== 'Tab' || !panel) {
        return
      }
      const focusable = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (focusable.length === 0) {
        return
      }
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown, true)
    const { overflow } = document.body.style
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKeyDown, true)
      document.body.style.overflow = overflow
      previouslyFocused?.focus()
    }
  }, [open, onCloseRef])

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-4">
      <div
        className="absolute inset-0 bg-slate-900/50 backdrop-blur-sm motion-safe:animate-[fade-in_150ms_ease-out]"
        onClick={onClose}
        aria-hidden
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="relative flex max-h-[92dvh] w-full max-w-md flex-col rounded-t-3xl border border-slate-200 bg-white shadow-2xl motion-safe:animate-[sheet-up_220ms_cubic-bezier(0.16,1,0.3,1)] sm:max-h-[88dvh] sm:rounded-3xl dark:border-slate-800 dark:bg-slate-900"
      >
        {/* Grab handle (mobile affordance) */}
        <div className="flex justify-center pt-2.5 sm:hidden" aria-hidden>
          <span className="h-1.5 w-10 rounded-full bg-slate-300 dark:bg-slate-700" />
        </div>

        <div className="flex items-start justify-between gap-4 px-5 pt-3 pb-4 sm:pt-5">
          <div className="flex min-w-0 items-start gap-3">
            {icon ? (
              <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-violet-100 text-violet-700 dark:bg-violet-950 dark:text-violet-300">
                {icon}
              </span>
            ) : null}
            <div className="min-w-0">
              <h2 className="text-lg font-semibold leading-tight">{title}</h2>
              {description ? (
                <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">{description}</p>
              ) : null}
            </div>
          </div>
          <Button variant="ghost" size="icon" aria-label="Close" onClick={onClose}>
            <X className="h-4 w-4" aria-hidden />
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5">{children}</div>

        {footer ? (
          <div className="sticky bottom-0 border-t border-slate-200 bg-white/95 px-5 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] backdrop-blur dark:border-slate-800 dark:bg-slate-900/95">
            {footer}
          </div>
        ) : null}
      </div>
    </div>
  )
}
