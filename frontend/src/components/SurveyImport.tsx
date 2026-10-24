import { useState, type ChangeEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { ImportResult } from '../api/types'

const MAX_BYTES = 1_000_000

/** Dış kaynaklı anket sonuçlarını CSV ile içe aktarır; satır bazlı hata raporu gösterir. */
export function SurveyImport({ surveyId, onImported }: { surveyId: string; onImported: () => void }) {
  const [fileName, setFileName] = useState<string | null>(null)
  const [csvText, setCsvText] = useState<string | null>(null)
  const [consent, setConsent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)

  async function onFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    setResult(null)
    setError(null)
    if (!file) return
    if (file.size > MAX_BYTES) {
      setCsvText(null)
      setFileName(null)
      return setError('Dosya çok büyük (en fazla 1 MB).')
    }
    setFileName(file.name)
    setCsvText(await file.text())
  }

  async function submit() {
    if (!csvText) return
    setBusy(true)
    setError(null)
    setResult(null)
    try {
      const res = await api.post<ImportResult>(`/api/surveys/${surveyId}/import`, { csvText, consentConfirmed: consent })
      setResult(res)
      if (res.imported > 0) onImported()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'İçe aktarma başarısız oldu.')
    } finally {
      setBusy(false)
    }
  }

  async function template() {
    try {
      await api.download(`/api/surveys/${surveyId}/import-template`, 'anket-iceaktarma-sablonu.csv')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Şablon indirilemedi.')
    }
  }

  return (
    <>
      <ol className="steps">
        <li>
          <button type="button" className="btn btn-small" onClick={() => void template()}>
            Şablonu indir
          </button>{' '}
          ve sonuçları şablondaki sütunlara doldurun. Excel kullanıyorsanız "CSV (noktalı virgülle ayrılmış)" olarak
          kaydedin.
        </li>
        <li>
          Sütunlar: <strong>Öğrenci No</strong> ve her soru için bir sütun. Seçim sorularında seçenek metnini yazın;
          çoklu seçimde seçenekleri <code>|</code> ile ayırın. Sayı sorularında ondalık için virgül ya da nokta
          kullanabilirsiniz.
        </li>
        <li>Yalnızca size bağlı ve hedef kitledeki öğrenciler içe aktarılır; hatalı satırlar atlanıp raporlanır.</li>
      </ol>

      <label className="field">
        <span>CSV dosyası</span>
        <input type="file" accept=".csv,text/csv" onChange={(e) => void onFile(e)} />
      </label>

      <label className="check consent-check">
        <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
        Bu verilerin öğrencilerin açık rızasıyla toplandığını onaylıyorum (KVKK).
      </label>

      <button type="button" className="btn btn-primary" disabled={!csvText || !consent || busy} onClick={() => void submit()}>
        {busy ? 'İçe aktarılıyor…' : fileName ? `"${fileName}" dosyasını içe aktar` : 'İçe aktar'}
      </button>

      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}

      {result && (
        <div className="import-result" role="status">
          <p className={result.errors.length === 0 ? 'form-ok' : 'form-warn'}>
            {result.imported} satır içe aktarıldı
            {result.errors.length > 0 ? `, ${result.errors.length} hata bulundu (ilgili satırlar atlandı).` : '.'}
          </p>
          {result.errors.length > 0 && (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Satır</th>
                    <th>Öğrenci</th>
                    <th>Hata</th>
                  </tr>
                </thead>
                <tbody>
                  {result.errors.map((e, i) => (
                    <tr key={`${e.row}-${i}`}>
                      <td>{e.row}</td>
                      <td>{e.studentNo ?? '-'}</td>
                      <td className="wrap-cell">{e.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </>
  )
}
