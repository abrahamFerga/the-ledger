import { z } from 'zod'
import { TRANSACTION_DIRECTIONS } from '../../api/types'

/**
 * The editable confirm form behind the quick-add bar (epic 9). The parsed AI draft pre-fills it; the
 * user can correct any field before confirming. On confirm we map these values to a
 * `ManualTransactionRequest` and create through the existing manual-create path — nothing persists
 * until this form is submitted (ADR-0011, confirm-before-persist).
 */
export const confirmDraftSchema = z.object({
  accountId: z.string().min(1, 'Choose an account'),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date'),
  description: z.string().trim().min(1, 'Description is required').max(200),
  amount: z.coerce.number().positive('Amount must be greater than 0'),
  direction: z.enum(TRANSACTION_DIRECTIONS),
})

export type ConfirmDraftValues = z.infer<typeof confirmDraftSchema>

/** WhatsApp opt-in form: a phone number in international format (digits only, 8–15 long). */
export const whatsappOptInSchema = z.object({
  phoneNumber: z
    .string()
    .trim()
    .regex(/^\+?\d{8,15}$/, 'Enter the number in international format, e.g. 5215512345678'),
  defaultAccountId: z.string().optional(),
})

export type WhatsAppOptInValues = z.infer<typeof whatsappOptInSchema>
