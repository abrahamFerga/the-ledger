/**
 * Hand-rolled, dependency-free line chart (SVG). Plots a single numeric series over labelled points.
 * Responsive via viewBox; accessible via role="img" + a text summary.
 */
import { useId } from 'react'

export interface LinePoint {
  label: string
  value: number
}

export function LineChart({
  points,
  height = 160,
  format,
  ariaLabel,
}: {
  points: LinePoint[]
  height?: number
  format: (n: number) => string
  ariaLabel: string
}) {
  const gradientId = useId()
  const width = 320
  const padX = 8
  const padY = 12

  if (points.length === 0) {
    return null
  }

  const values = points.map((p) => p.value)
  const min = Math.min(0, ...values)
  const max = Math.max(0, ...values)
  const range = max - min || 1
  const stepX = points.length > 1 ? (width - padX * 2) / (points.length - 1) : 0

  const coords = points.map((p, i) => {
    const x = padX + i * stepX
    const y = padY + (1 - (p.value - min) / range) * (height - padY * 2)
    return { x, y }
  })

  const linePath = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x} ${c.y}`).join(' ')
  const areaPath =
    `${linePath} L ${coords[coords.length - 1].x} ${height - padY} L ${coords[0].x} ${height - padY} Z`

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      className="w-full"
      role="img"
      aria-label={`${ariaLabel}. Latest: ${format(values[values.length - 1])}.`}
      preserveAspectRatio="none"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#7c3aed" stopOpacity="0.25" />
          <stop offset="100%" stopColor="#7c3aed" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={areaPath} fill={`url(#${gradientId})`} />
      <path d={linePath} fill="none" stroke="#7c3aed" strokeWidth={2} strokeLinejoin="round" />
      {coords.map((c, i) => (
        <circle key={i} cx={c.x} cy={c.y} r={2.5} fill="#7c3aed" />
      ))}
    </svg>
  )
}
