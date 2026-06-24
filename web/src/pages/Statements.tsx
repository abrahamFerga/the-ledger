export function Statements() {
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Upload a statement</h1>

      <label className="flex h-44 cursor-pointer flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-slate-300 text-slate-500 dark:border-slate-700">
        <span className="text-3xl" aria-hidden>
          📄
        </span>
        <span className="text-sm font-medium">Tap to choose a PDF or CSV</span>
        <span className="text-xs text-slate-400">or take a photo of a statement</span>
        <input type="file" accept=".pdf,.csv,image/*" capture="environment" className="hidden" />
      </label>

      <p className="text-xs text-slate-400">
        Supported: BBVA, Santander, Banorte, and digital banks (Nu / Hey / Klar). Card numbers are
        masked on import; nothing leaves your household.
      </p>
    </div>
  )
}
