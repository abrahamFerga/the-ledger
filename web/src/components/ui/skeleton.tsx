/**
 * Skeleton — content-shaped loading placeholders (not a spinner). A soft shimmer sweeps across the
 * block so a load reads as "content arriving" rather than "stuck". Reduced-motion users get the
 * static block (the shimmer is motion-safe only).
 */
import { cn } from '../../lib/utils'

export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-lg bg-slate-100 dark:bg-slate-800/70',
        'motion-safe:after:absolute motion-safe:after:inset-0 motion-safe:after:-translate-x-full',
        'motion-safe:after:animate-[shimmer_1.6s_infinite]',
        'motion-safe:after:bg-gradient-to-r motion-safe:after:from-transparent motion-safe:after:via-white/60 motion-safe:after:to-transparent',
        'dark:motion-safe:after:via-white/10',
        className,
      )}
      aria-hidden
    />
  )
}

/** A row of skeleton cards sized for the review queue / receipts list while data loads. */
export function SkeletonCards({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-3" role="status" aria-live="polite" aria-busy="true">
      <span className="sr-only">Loading…</span>
      {Array.from({ length: count }, (_, i) => (
        <div
          key={i}
          className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800"
        >
          <div className="flex items-center justify-between gap-3">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-4 w-16" />
          </div>
          <div className="mt-3 flex items-center gap-2">
            <Skeleton className="h-6 w-20 rounded-full" />
            <Skeleton className="h-6 w-24 rounded-full" />
          </div>
        </div>
      ))}
    </div>
  )
}
