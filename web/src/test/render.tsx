import { type ReactElement } from 'react'
import { render, type RenderResult } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { createQueryClient } from '../api/queryClient'
import { ToastProvider } from '../components/ui/toast'

/** Render a component inside the app's real providers (Query + Toast + Router) for component tests. */
export function renderWithProviders(ui: ReactElement): RenderResult {
  const client = createQueryClient()
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}
