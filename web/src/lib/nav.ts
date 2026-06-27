import type { ComponentType } from 'react'
import {
  AlertTriangle,
  Camera,
  Home,
  Landmark,
  ListChecks,
  PiggyBank,
  Plug,
  ClipboardCheck,
  Target,
  TrendingUp,
  Upload,
} from 'lucide-react'

export interface NavItem {
  to: string
  label: string
  Icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>
  /** Shown in the thumb-reachable mobile bottom bar (kept to ~5 to stay uncluttered). */
  primary: boolean
}

/**
 * Navigation surface. The desktop sidebar shows every item; the mobile bottom bar shows the
 * `primary` ones (one-handed primary flows). Order is the same in both.
 */
export const nav: NavItem[] = [
  { to: '/', label: 'Home', Icon: Home, primary: true },
  { to: '/transactions', label: 'Ledger', Icon: ListChecks, primary: true },
  // AI-first capture flows are celebrated as primary, thumb-reachable actions (epic 9).
  { to: '/capture', label: 'Scan', Icon: Camera, primary: true },
  { to: '/review', label: 'Review', Icon: ClipboardCheck, primary: true },
  { to: '/accounts', label: 'Accounts', Icon: Landmark, primary: false },
  { to: '/budgets', label: 'Budgets', Icon: Target, primary: false },
  { to: '/goals', label: 'Goals', Icon: PiggyBank, primary: false },
  { to: '/insights', label: 'Insights', Icon: TrendingUp, primary: false },
  { to: '/alerts', label: 'Alerts', Icon: AlertTriangle, primary: false },
  { to: '/statements', label: 'Upload', Icon: Upload, primary: false },
  { to: '/integrations', label: 'Integrations', Icon: Plug, primary: false },
]

export const primaryNav = nav.filter((item) => item.primary)
