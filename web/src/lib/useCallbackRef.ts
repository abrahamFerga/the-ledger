import { useCallback, useEffect, useRef } from 'react'

/**
 * Returns a stable function identity that always calls the latest `callback`. Lets effects depend on
 * the returned ref without re-subscribing every time the callback prop changes.
 */
export function useCallbackRef<Args extends unknown[], R>(
  callback: (...args: Args) => R,
): (...args: Args) => R {
  const ref = useRef(callback)
  useEffect(() => {
    ref.current = callback
  })
  return useCallback((...args: Args) => ref.current(...args), [])
}
