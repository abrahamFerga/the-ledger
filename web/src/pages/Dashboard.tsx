import { Link } from 'react-router-dom'
import { useLedger, useMonthlyTotals, useNetWorth } from '../api/hooks'
import { formatDate, formatMoney } from '../lib/utils'

export function Dashboard() {
  const netWorth = useNetWorth()
  const monthly = useMonthlyTotals()
  const recent = useLedger({ confirmedOnly: false })

  const now = new Date()
  const thisMonth = (monthly.data ?? []).find(
    (m) => m.year === now.getFullYear() && m.month === now.getMonth() + 1,
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Overview</h1>

      <section className="rounded-2xl bg-gradient-to-br from-violet-600 to-indigo-600 p-5 text-white shadow-sm">
        <p className="text-sm opacity-80">Net worth</p>
        <p className="mt-1 text-3xl font-bold tabular-nums">
          {netWorth.isLoading ? '…' : formatMoney(netWorth.data?.total ?? 0)}{' '}
          <span className="text-base font-normal opacity-80">MXN</span>
        </p>
        <p className="mt-2 text-xs opacity-80">
          {(netWorth.data?.accounts.length ?? 0) === 0
            ? 'Upload a statement to start tracking.'
            : `Across ${netWorth.data?.accounts.length} account(s).`}
        </p>
      </section>

      <div className="grid grid-cols-2 gap-3">
        <Stat label="Spent this month" value={formatMoney(thisMonth?.expense ?? 0)} />
        <Stat label="Income this month" value={formatMoney(thisMonth?.income ?? 0)} />
      </div>

      <section className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
        <div className="mb-2 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-500">Recent transactions</h2>
          <Link to="/transactions" className="text-xs font-medium text-violet-600 hover:underline">
            View all
          </Link>
        </div>
        {recent.isLoading ? (
          <p className="text-sm text-slate-400">Loading…</p>
        ) : (recent.data?.length ?? 0) === 0 ? (
          <p className="text-sm text-slate-400">
            No transactions yet — upload a bank statement (PDF/CSV) or add one manually.
          </p>
        ) : (
          <ul className="divide-y divide-slate-100 dark:divide-slate-800">
            {(recent.data ?? []).slice(0, 5).map((tx) => (
              <li key={tx.id} className="flex items-center justify-between gap-3 py-1.5 text-sm">
                <div className="min-w-0">
                  <p className="truncate">{tx.description}</p>
                  <p className="text-xs text-slate-400">{formatDate(tx.date)}</p>
                </div>
                <span
                  className={`shrink-0 tabular-nums ${
                    tx.direction === 'Credit' ? 'text-emerald-600' : ''
                  }`}
                >
                  {tx.direction === 'Credit' ? '+' : '−'}
                  {formatMoney(tx.amount, tx.currency)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
      <p className="text-xs text-slate-500">{label}</p>
      <p className="mt-1 text-xl font-semibold tabular-nums">{value}</p>
    </div>
  )
}
