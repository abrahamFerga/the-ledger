export interface NavItem {
  to: string
  label: string
  icon: string
}

export const nav: NavItem[] = [
  { to: '/', label: 'Home', icon: '🏠' },
  { to: '/accounts', label: 'Accounts', icon: '💳' },
  { to: '/statements', label: 'Upload', icon: '📄' },
  { to: '/budgets', label: 'Budgets', icon: '🎯' },
]
