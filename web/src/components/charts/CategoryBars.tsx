/** Hand-rolled horizontal bar breakdown (no chart dependency). Each row is a category share. */
import { formatMoney } from '../../lib/utils'

export interface CategoryBar {
  label: string
  value: number
}

const PALETTE = ['#7c3aed', '#6366f1', '#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#ec4899', '#8b5cf6']

export function CategoryBars({ rows }: { rows: CategoryBar[] }) {
  const max = Math.max(...rows.map((r) => r.value), 1)
  return (
    <ul className="space-y-2">
      {rows.map((row, i) => {
        const pct = Math.round((row.value / max) * 100)
        return (
          <li key={`${row.label}-${i}`}>
            <div className="mb-0.5 flex items-center justify-between text-sm">
              <span className="truncate">{row.label}</span>
              <span className="tabular-nums text-slate-500">{formatMoney(row.value)}</span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
              <div
                className="h-full rounded-full"
                style={{ width: `${pct}%`, backgroundColor: PALETTE[i % PALETTE.length] }}
              />
            </div>
          </li>
        )
      })}
    </ul>
  )
}
