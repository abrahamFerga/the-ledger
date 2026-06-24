export function Dashboard() {
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Overview</h1>

      <section className="rounded-2xl bg-gradient-to-br from-violet-600 to-indigo-600 p-5 text-white shadow-sm">
        <p className="text-sm opacity-80">Net worth</p>
        <p className="mt-1 text-3xl font-bold">
          $0.00 <span className="text-base font-normal opacity-80">MXN</span>
        </p>
        <p className="mt-2 text-xs opacity-80">Upload a statement to start tracking.</p>
      </section>

      <div className="grid grid-cols-2 gap-3">
        <Stat label="Spent this month" value="$0" />
        <Stat label="Budget remaining" value="$0" />
      </div>

      <section className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
        <h2 className="mb-2 text-sm font-semibold text-slate-500">Recent transactions</h2>
        <p className="text-sm text-slate-400">
          No transactions yet — upload a bank statement (PDF/CSV) or add one manually.
        </p>
      </section>
    </div>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
      <p className="text-xs text-slate-500">{label}</p>
      <p className="mt-1 text-xl font-semibold">{value}</p>
    </div>
  )
}
