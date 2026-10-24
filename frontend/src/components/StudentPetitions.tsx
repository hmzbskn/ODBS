import { useRef, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { Petition, PetitionType } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { PETITION_TYPE_LABELS } from './labels'
import { PetitionCard } from './PetitionCard'
import { useAsync } from './useAsync'

const MAX_BYTES = 5 * 1024 * 1024
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.docx'

/** Öğrenci: talep/dilekçe açar (isteğe bağlı dosya ekiyle) ve taleplerinin durumunu izler. */
export function StudentPetitions() {
  const { state, retry } = useAsync(() => api.get<Petition[]>('/api/petitions'), [])
  const [type, setType] = useState<PetitionType>('KayitDondurma')
  const [description, setDescription] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)

  function onFile(f: File | undefined) {
    setMessage(null)
    if (f && f.size > MAX_BYTES) {
      setFile(null)
      if (fileInput.current) fileInput.current.value = ''
      return setMessage({ text: 'Dosya çok büyük (en fazla 5 MB).', ok: false })
    }
    setFile(f ?? null)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setMessage(null)
    const form = new FormData()
    form.append('type', type)
    form.append('description', description)
    if (file) form.append('file', file)
    try {
      await api.postForm('/api/petitions', form)
      setMessage({ text: 'Talebiniz danışmanınıza iletildi.', ok: true })
      setDescription('')
      setFile(null)
      if (fileInput.current) fileInput.current.value = ''
      retry()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'Talep gönderilemedi.', ok: false })
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <form className="card" onSubmit={submit}>
        <h2>Yeni talep</h2>
        <div className="form-grid">
          <label className="field">
            <span>Talep türü</span>
            <select value={type} onChange={(e) => setType(e.target.value as PetitionType)}>
              {(Object.keys(PETITION_TYPE_LABELS) as PetitionType[]).map((t) => (
                <option key={t} value={t}>
                  {PETITION_TYPE_LABELS[t]}
                </option>
              ))}
            </select>
          </label>
          <label className="field span-2">
            <span>Açıklama</span>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} minLength={10} maxLength={2000} required />
          </label>
          <label className="field span-2">
            <span>Ek dosya (isteğe bağlı: PDF, PNG, JPG, DOCX · en fazla 5 MB)</span>
            <input ref={fileInput} type="file" accept={ACCEPT} onChange={(e) => onFile(e.target.files?.[0])} />
          </label>
        </div>
        {message && (
          <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
            {message.text}
          </p>
        )}
        <div className="btn-row submit-row">
          <button type="submit" className="btn btn-primary" disabled={busy || description.trim().length < 10}>
            {busy ? 'Gönderiliyor…' : 'Talebi gönder'}
          </button>
        </div>
      </form>

      <h2 className="section-title">Taleplerim</h2>
      {state.status === 'loading' && <Loading />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' &&
        (state.data.length === 0 ? (
          <div className="card">
            <EmptyState title="Henüz talebiniz yok" />
          </div>
        ) : (
          state.data.map((p) => <PetitionCard key={p.id} petition={p} />)
        ))}
    </>
  )
}
