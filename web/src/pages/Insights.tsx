import { useState } from 'react'
import { Download } from 'lucide-react'
import { useMonthlyTotals, useNetWorth, useSpending } from '../api/hooks'
import { insightsApi } from '../api/endpoints'
import { errorMessage } from '../api/hooks'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { MonthPicker } from '../components/ui/month-picker'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { LineChart } from '../components/charts/LineChart'
import { CategoryBars } from '../components/charts/CategoryBars'
import { useToast } from '../components/ui/toast'
import { downloadBlob, formatMoney, monthLabel } from '../lib/utils'

export function Insights() {
  const now = new Date()
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 })
  const toast = useToast()
  const [exporting, setExporting] = useState(false)

  const netWorth = useNetWorth()
  const monthly = useMonthlyTotals()
  const spending = useSpending(period.year, period.month)

  async function exportCsv() {
    setExporting(true)
    try {
      const blob = await insightsApi.exportCsv()
      downloadBlob(blob, 'transactions.csv')
      toast.success('Export downloaded')
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setExporting(false)
    }
  }

  const netSeries = (monthly.data ?? []).map((m) => ({
    label: monthLabel(m.year, m.month),
    value: m.net,
  }))

  return (
    <div className="space-y-4">
      <PageHeader
        title="Insights"
        subtitle="Net worth, monthly trend, and where the money goes."
        action={
          <Button variant="secondary" onClick={exportCsv} disabled={exporting}>
            <Download className="h-4 w-4" aria-hidden />
            {exporting ? 'Exporting…' : 'Export CSV'}
          </Button>
        }
      />

      <section className="rounded-2xl bg-gradient-to-br from-violet-600 to-indigo-600 p-5 text-white shadow-sm">
        <p className="text-sm opacity-80">Net worth</p>
        {netWorth.isLoading ? (
          <p className="mt-1 text-3xl font-bold">…</p>
        ) : netWorth.isError ? (
          <p className="mt-1 text-sm opacity-90">Could not load net worth.</p>
        ) : (
          <p className="mt-1 text-3xl font-bold tabular-nums">{formatMoney(netWorth.data?.total ?? 0)}</p>
        )}
        <p className="mt-2 text-xs opacity-80">
          Across {netWorth.data?.accounts.length ?? 0} account
          {(netWorth.data?.accounts.length ?? 0) === 1 ? '' : 's'}.
        </p>
      </section>

      <Card>
        <h2 className="mb-2 text-sm font-semibold text-slate-500">Monthly net (income − expense)</h2>
        {monthly.isLoading ? (
          <LoadingState label="Loading trend" />
        ) : monthly.isError ? (
          <ErrorState message="Could not load the trend." onRetry={() => monthly.refetch()} />
        ) : netSeries.length > 0 ? (
          <LineChart points={netSeries} format={(n) => formatMoney(n)} ariaLabel="Monthly net trend" />
        ) : (
          <p className="py-6 text-center text-sm text-slate-400">
            No history yet — add transactions to see the trend.
          </p>
        )}
      </Card>

      <Card>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-500">Spending by category</h2>
          <MonthPicker
            year={period.year}
            month={period.month}
            onChange={(year, month) => setPeriod({ year, month })}
          />
        </div>
        {spending.isLoading ? (
          <LoadingState label="Loading spending" />
        ) : spending.isError ? (
          <ErrorState message="Could not load spending." onRetry={() => spending.refetch()} />
        ) : spending.data && spending.data.length > 0 ? (
          <CategoryBars
            rows={spending.data.map((s) => ({ label: s.categoryName, value: s.total }))}
          />
        ) : (
          <EmptyState title="No spending this month" description="Pick another month or add transactions." />
        )}
      </Card>
    </div>
  )
}
