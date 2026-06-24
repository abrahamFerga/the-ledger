import { type ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { nav } from '../lib/nav'

/**
 * Mobile-first responsive shell (feature #10): a desktop sidebar collapses to a thumb-reachable
 * bottom navigation bar under `md`. The top bar carries the household switch and the assistant
 * trigger. Every primary flow is reachable one-handed on a phone.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-dvh bg-slate-50 text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white/80 px-4 backdrop-blur dark:border-slate-800 dark:bg-slate-900/80">
        <div className="flex items-center gap-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-600 font-bold text-white">L</div>
          <span className="font-semibold">the-ledger</span>
        </div>
        <div className="flex items-center gap-2">
          <button className="rounded-lg border border-slate-200 px-3 py-1.5 text-sm dark:border-slate-700">
            Household ▾
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
            {nav.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm ${
                    isActive
                      ? 'bg-violet-50 font-medium text-violet-700 dark:bg-violet-950 dark:text-violet-300'
                      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
                  }`
                }
              >
                <span aria-hidden>{item.icon}</span>
                {item.label}
              </NavLink>
            ))}
          </nav>
        </aside>

        <main className="min-w-0 flex-1 p-4 pb-24 md:pb-8">{children}</main>
      </div>

      <nav className="fixed inset-x-0 bottom-0 z-20 grid grid-cols-4 border-t border-slate-200 bg-white/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden dark:border-slate-800 dark:bg-slate-900/95">
        {nav.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 py-2 text-xs ${
                isActive ? 'text-violet-600 dark:text-violet-400' : 'text-slate-500'
              }`
            }
          >
            <span className="text-lg" aria-hidden>
              {item.icon}
            </span>
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
