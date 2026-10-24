import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'

export type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T }

/** Bir API çağrısını yükleniyor / hata / hazır durumlarıyla yönetir. Yalnızca `retry` ile yenilenirken
 * (girdiler aynıyken) mevcut veri ekranda kalır; girdiler değişince yükleniyor durumuna geçer. */
export function useAsync<T>(load: () => Promise<T>, deps: readonly unknown[]) {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const depsKey = JSON.stringify(deps)
  const lastDepsKey = useRef(depsKey)

  useEffect(() => {
    let cancelled = false
    const sameInputs = lastDepsKey.current === depsKey
    lastDepsKey.current = depsKey
    setState((prev) => (sameInputs && prev.status === 'ready' ? prev : { status: 'loading' }))
    load()
      .then((data) => {
        if (!cancelled) setState({ status: 'ready', data })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        const message = error instanceof ApiError ? error.message : 'Beklenmeyen bir hata oluştu.'
        setState({ status: 'error', message })
      })
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attempt, depsKey])

  const retry = useCallback(() => setAttempt((n) => n + 1), [])
  return { state, retry }
}
