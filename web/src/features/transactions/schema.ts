import { z } from 'zod'
import { TRANSACTION_DIRECTIONS } from '../../api/types'

export const manualTransactionSchema = z.object({
  accountId: z.string().min(1, 'Choose an account'),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date'),
  description: z.string().trim().min(1, 'Description is required').max(200),
  // Coerce the numeric string from the <input type="number"> into a number, > 0.
  amount: z.coerce.number().positive('Amount must be greater than 0'),
  direction: z.enum(TRANSACTION_DIRECTIONS),
})

export type ManualTransactionFormValues = z.infer<typeof manualTransactionSchema>
