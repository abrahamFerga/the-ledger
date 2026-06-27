import { useState } from 'react'
import { Bell, Check } from 'lucide-react'
import { useAlerts, useDismissAlert } from '../api/hooks'
import { alertsApi } from '../api/endpoints'
import { errorMessage } from '../api/hooks'
import type { AlertDto } from '../api/types'
import { PageHeader, Card } from '../components/ui/page'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { EmptyState, ErrorState, LoadingState } from '../components/ui/states'
import { useToast } from '../components/ui/toast'
import { formatDate } from '../lib/utils'

export function Alerts() {
  const [includeResolved, setIncludeResolved] = useState(false)
  const alerts = useAlerts(includeResolved)
  const dismiss = useDismissAlert(includeResolved)
  const toast = useToast()
  const [scanning, setScanning] = useState(false)

  async function runScan() {
    setScanning(true)
    try {
      const result = await alertsApi.scan()
      toast.success(result.raised > 0 ? `${result.raised} new alert(s)` : 'No new alerts')
      alerts.refetch()
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setScanning(false)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Alerts"
        subtitle="Bill reminders and spending anomalies for your household."
        action={
          <Button variant="secondary" onClick={runScan} disabled={scanning}>
            <Bell className="h-4 w-4" aria-hidden />
            {scanning ? 'Scanning…' : 'Scan now'}
          </Button>
        }
      />

      <label className="flex items-center gap-1.5 text-sm text-slate-600 dark:text-slate-300">
        <input
          type="checkbox"
          className="h-4 w-4 rounded border-slate-300 text-violet-600 focus:ring-violet-500"
          checked={includeResolved}
          onChange={(e) => setIncludeResolved(e.target.checked)}
        />
        Include dismissed
      </label>

      {alerts.isLoading ? (
        <LoadingState label="Loading alerts" />
      ) : alerts.isError ? (
        <ErrorState message="Could not load alerts." onRetry={() => alerts.refetch()} />
      ) : alerts.data && alerts.data.length > 0 ? (
        <ul className="space-y-2">
          {alerts.data.map((alert) => (
            <li key={alert.id}>
              <AlertRow
                alert={alert}
                onDismiss={() => dismiss.mutate(alert.id)}
                dismissing={dismiss.isPending}
              />
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          title="No alerts"
          description="You're all caught up. Run a scan to check for new bill reminders or anomalies."
        />
      )}
    </div>
  )
}

const STATUS_TONE: Record<string, 'amber' | 'green' | 'neutral'> = {
  Open: 'amber',
  Resolved: 'green',
  Dismissed: 'neutral',
}

function AlertRow({
  alert,
  onDismiss,
  dismissing,
}: {
  alert: AlertDto
  onDismiss: () => void
  dismissing: boolean
}) {
  const isOpen = alert.status === 'Open'
  return (
    <Card className="flex items-center justify-between gap-3">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <Badge tone="violet">{alert.type}</Badge>
          <Badge tone={STATUS_TONE[alert.status] ?? 'neutral'}>{alert.status}</Badge>
        </div>
        <p className="mt-1 truncate text-sm">{alert.message}</p>
        <p className="text-xs text-slate-400">{formatDate(alert.createdAt)}</p>
      </div>
      {isOpen ? (
        <Button
          variant="secondary"
          size="sm"
          onClick={onDismiss}
          disabled={dismissing}
          aria-label="Mark alert as seen"
        >
          <Check className="h-4 w-4" aria-hidden /> Seen
        </Button>
      ) : null}
    </Card>
  )
}
