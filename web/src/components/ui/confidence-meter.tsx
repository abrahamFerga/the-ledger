/**
 * ConfidenceMeter — surfaces an AI parse/OCR confidence (0..1) as a labelled bar. This is an
 * AI-first product, so we always show how sure the model is: high confidence reads calm green,
 * medium amber, low rose ("worth a check"). The numeric percentage is announced for screen readers.
 */
import { cn } from '../../lib/utils'

type Tier = 'high' | 'medium' | 'low'

function tierFor(confidence: number): Tier {
  if (confidence >= 0.85) {
    return 'high'
  }
  if (confidence >= 0.6) {
    return 'medium'
  }
  return 'low'
}

const BAR: Record<Tier, string> = {
  high: 'bg-emerald-500',
  medium: 'bg-amber-500',
  low: 'bg-rose-500',
}

const LABEL: Record<Tier, string> = {
  high: 'High confidence',
  medium: 'Medium confidence',
  low: 'Low — please double-check',
}

export function ConfidenceMeter({
  confidence,
  className,
}: {
  confidence: number
  className?: string
}) {
  const pct = Math.round(Math.max(0, Math.min(1, confidence)) * 100)
  const tier = tierFor(confidence)
  return (
    <div className={cn('space-y-1', className)}>
      <div className="flex items-center justify-between text-xs">
        <span className="font-medium text-slate-600 dark:text-slate-300">AI confidence</span>
        <span className="tabular-nums text-slate-500">{pct}%</span>
      </div>
      <div
        className="h-1.5 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800"
        role="meter"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`AI confidence: ${pct} percent, ${LABEL[tier]}`}
      >
        <div
          className={cn('h-full rounded-full transition-[width] duration-500', BAR[tier])}
          style={{ width: `${pct}%` }}
        />
      </div>
      <p className="text-xs text-slate-400">{LABEL[tier]}</p>
    </div>
  )
}
