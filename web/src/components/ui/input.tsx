import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'
import { fieldClass } from './field-styles'

export type InputProps = InputHTMLAttributes<HTMLInputElement>

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { className, ...props },
  ref,
) {
  return <input ref={ref} className={cn(fieldClass, className)} {...props} />
})
