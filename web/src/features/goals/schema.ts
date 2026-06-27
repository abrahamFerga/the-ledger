import { z } from 'zod'

export const goalSchema = z.object({
  name: z.string().trim().min(1, 'Name is required').max(120),
  targetAmount: z.coerce.number().positive('Target must be greater than 0'),
  targetDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date').optional().or(z.literal('')),
})

export type GoalFormValues = z.infer<typeof goalSchema>

export const contributeSchema = z.object({
  amount: z.coerce.number().positive('Amount must be greater than 0'),
})

export type ContributeFormValues = z.infer<typeof contributeSchema>
