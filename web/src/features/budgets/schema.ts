import { z } from 'zod'

export const budgetSchema = z.object({
  categoryId: z.string().min(1, 'Choose a category'),
  targetAmount: z.coerce.number().positive('Target must be greater than 0'),
  rollover: z.boolean(),
})

export type BudgetFormValues = z.infer<typeof budgetSchema>
