import { type ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { nav, primaryNav } from '../lib/nav'
import { useHousehold } from '../api/hooks'

/**
 * Mobile-first responsive shell (feature #10): a desktop sidebar collapses to a thumb-reachable
 * bottom navigation bar under `md`. The top bar carries the household switch and the assistant
 * trigger. Every primary flow is reachable one-handed on a phone.
 *
 * Extended for feature #55: the sidebar lists every page; the bottom bar shows the primary flows.
 * The household label is now driven by the current-household query.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const household = useHousehold()
  const householdName = household.data?.name ?? 'Household'

  return (
    <div className="min-h-dvh bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white/80 px-4 backdrop-blur dark:border-slate-800 dark:bg-slate-900/80">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-600 font-bold text-white">L</div>
          <span className="font-semibold">the-ledger</span>
        </div>
        <div className="flex items-center gap-2">
          <button
            className="max-w-[10rem] truncate rounded-lg border border-slate-200 px-3 py-1.5 text-sm dark:border-slate-700"
            title="Switch household"
          >
            {householdName} ▾
          </button>
          <button
            aria-label="Open assistant"
            className="rounded-lg bg-violet-600 px-3 py-1.5 text-sm font-medium text-white"
          >
            Ask
          </button>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-6xl">
        <aside className="hidden w-56 shrink-0 border-r border-slate-200 p-3 md:block dark:border-slate-800">
          <nav className="flex flex-col gap-1">
            {nav.map(({ to, label, Icon }) => (
              <NavLink
                key={to}
                to={to}
                end={to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm ${
                    isActive
                      ? 'bg-violet-50 font-medium text-violet-700 dark:bg-violet-950 dark:text-violet-300'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
                  }`
                }
              >
                <Icon className="h-4 w-4" aria-hidden />
                {label}
              </NavLink>
            ))}
          </nav>
        </aside>

        <main className="min-w-0 flex-1 p-4 pb-24 md:pb-8">{children}</main>
      </div>

      <nav
        className="fixed inset-x-0 bottom-0 z-20 grid border-t border-slate-200 bg-white/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden dark:border-slate-800 dark:bg-slate-900/95"
        style={{ gridTemplateColumns: `repeat(${primaryNav.length}, minmax(0, 1fr))` }}
      >
        {primaryNav.map(({ to, label, Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 py-2 text-xs ${
                isActive ? 'text-violet-600 dark:text-violet-400' : 'text-slate-500'
              }`
            }
          >
            <Icon className="h-5 w-5" aria-hidden />
            {label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
