import { forwardRef, type SelectHTMLAttributes } from 'react'
import { cn } from '../../lib/utils'
import { fieldClass } from './field-styles'

export type SelectProps = SelectHTMLAttributes<HTMLSelectElement>

/** Native-select primitive — accessible, keyboard-friendly, themed like the other fields. */
export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { className, children, ...props },
  ref,
) {
  return (
    <select ref={ref} className={cn(fieldClass, 'appearance-none pr-8', className)} {...props}>
      {children}
    </select>
  )
})
