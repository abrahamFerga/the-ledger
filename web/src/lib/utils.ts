/** Small shared helpers: class-name merge + money/date formatting (MXN default, mobile-first). */

/** Join truthy class names. A tiny `clsx`/`cn` without the dependency. */
export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ')
}

/** Format a decimal amount as a currency string (MXN by default). */
export function formatMoney(amount: number, currency = 'MXN'): string {
  return new Intl.NumberFormat('es-MX', {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(amount)
}

/** Format a DateOnly ("YYYY-MM-DD") or ISO string as a short locale date. */
export function formatDate(value: string): string {
  // Parse the date-only part to avoid TZ shifting a "YYYY-MM-DD" to the previous day.
  const [datePart] = value.split('T')
  const [y, m, d] = datePart.split('-').map(Number)
  if (!y || !m || !d) {
    return value
  }
  return new Intl.DateTimeFormat('es-MX', { day: '2-digit', month: 'short', year: 'numeric' }).format(
    new Date(y, m - 1, d),
  )
}

/** Today as a DateOnly "YYYY-MM-DD" string in local time (for default form values). */
export function todayIso(): string {
  const now = new Date()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${m}-${d}`
}

/** Trigger a browser download of a Blob under the given filename. */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}

const MONTH_LABELS = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
]

/** Short "Mon YY" label for a 1-based year/month pair. */
export function monthLabel(year: number, month: number): string {
  return `${MONTH_LABELS[month - 1]} ${String(year).slice(2)}`
}
