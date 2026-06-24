export function Budgets() {
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Budgets</h1>
      <p className="text-sm text-slate-400">
        Set a monthly target per category and track spending against it. Budgets appear here once
        you have categorized transactions.
      </p>
      <div className="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
        <div className="mb-1 flex items-center justify-between text-sm">
          <span className="font-medium">Groceries</span>
          <span className="text-slate-400">$0 / $0</span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800">
          <div className="h-full w-0 rounded-full bg-violet-600" />
        </div>
      </div>
    </div>
  )
}
