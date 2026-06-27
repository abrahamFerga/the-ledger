/**
 * The shared Form pattern: react-hook-form + zod. Pages call `useZodForm(schema)` for a typed form,
 * then compose fields with `<FormField>` (label + control + validation message + a11y wiring).
 *
 * Example:
 *   const form = useZodForm(schema, { defaultValues })
 *   <form onSubmit={form.handleSubmit(onSubmit)}>
 *     <FormField label="Name" error={form.formState.errors.name?.message}>
 *       {(id) => <Input id={id} {...form.register('name')} />}
 *     </FormField>
 *   </form>
 */
import { useId, type ReactNode } from 'react'
import {
  useForm,
  type DefaultValues,
  type FieldValues,
  type UseFormProps,
  type UseFormReturn,
} from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import type { ZodType, input as ZodInput, output as ZodOutput } from 'zod'
import { Label } from './label'

/**
 * Build a typed react-hook-form bound to a zod schema. The form fields use the schema's *input*
 * type (so `z.coerce.*` fields can default to `undefined`/strings), while `handleSubmit` delivers
 * the parsed *output* type — exactly what callers want to send to the API.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function useZodForm<TSchema extends ZodType>(
  schema: TSchema,
  options?: Omit<UseFormProps<ZodInput<TSchema>>, 'resolver'> & {
    defaultValues?: DefaultValues<ZodInput<TSchema>>
  },
): UseFormReturn<ZodInput<TSchema>, unknown, ZodOutput<TSchema>> {
  return useForm<ZodInput<TSchema>, unknown, ZodOutput<TSchema>>({
    // The resolver's generics don't line up perfectly with arbitrary zod schemas; this cast is the
    // standard react-hook-form + zod bridge and is type-safe at the call sites.
    resolver: zodResolver(schema as ZodType<FieldValues>) as never,
    ...options,
  })
}

/**
 * A labelled form field. `children` receives the generated control id so the label's `htmlFor` and
 * the control's `id`/`aria-describedby` are linked for screen readers.
 */
export function FormField({
  label,
  error,
  hint,
  children,
}: {
  label: string
  error?: string
  hint?: string
  children: (id: string) => ReactNode
}) {
  const id = useId()
  const errorId = `${id}-error`
  const hintId = `${id}-hint`
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      {children(id)}
      {hint && !error ? (
        <p id={hintId} className="text-xs text-slate-400">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} role="alert" className="text-xs font-medium text-rose-600">
          {error}
        </p>
      ) : null}
    </div>
  )
}
