import { z } from 'zod'
import { ACCOUNT_TYPES } from '../../api/types'

export const accountSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(120),
  type: z.enum(ACCOUNT_TYPES),
  institution: z.string().trim().max(120).optional().or(z.literal('')),
  currency: z
    .string()
    .trim()
    .regex(/^[A-Za-z]{3}$/, 'Use a 3-letter ISO code')
    .optional()
    .or(z.literal('')),
  number: z
    .string()
    .trim()
    .regex(/^\d*$/, 'Digits only')
    .max(19, 'Too long')
    .optional()
    .or(z.literal('')),
})

export type AccountFormValues = z.infer<typeof accountSchema>
