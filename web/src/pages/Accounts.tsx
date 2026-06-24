export function Accounts() {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Accounts</h1>
        <button className="rounded-lg bg-violet-600 px-3 py-1.5 text-sm font-medium text-white">
          Add
        </button>
      </div>
      <p className="text-sm text-slate-400">
        No accounts yet. Add a checking, savings, card, or cash account — or upload a statement and
        we will create one for you.
      </p>
    </div>
  )
}
