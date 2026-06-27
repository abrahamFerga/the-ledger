import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from './button'

const MONTHS = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
]

/** A compact previous/next month stepper. `year`/`month` are 1-based; `onChange(year, month)`. */
export function MonthPicker({
  year,
  month,
  onChange,
}: {
  year: number
  month: number
  onChange: (year: number, month: number) => void
}) {
  function step(delta: number) {
    const zero = (year * 12 + (month - 1)) + delta
    onChange(Math.floor(zero / 12), (zero % 12) + 1)
  }

  return (
    <div className="inline-flex items-center gap-2 rounded-lg border border-slate-200 px-1 py-0.5 dark:border-slate-700">
      <Button variant="ghost" size="icon" aria-label="Previous month" onClick={() => step(-1)}>
        <ChevronLeft className="h-4 w-4" aria-hidden />
      </Button>
      <span className="min-w-[5.5rem] text-center text-sm font-medium tabular-nums">
        {MONTHS[month - 1]} {year}
      </span>
      <Button variant="ghost" size="icon" aria-label="Next month" onClick={() => step(1)}>
        <ChevronRight className="h-4 w-4" aria-hidden />
      </Button>
    </div>
  )
}
