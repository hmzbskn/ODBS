const SESSION_KEY = 'advisor.session'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let unauthorizedHandler: (() => void) | null = null

/** Oturum süresi dolduğunda (401) çağrılır; AuthProvider kullanıcıyı çıkışa yönlendirir. */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  unauthorizedHandler = handler
}

export function readStoredToken(): string | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    return raw ? (JSON.parse(raw) as { accessToken: string }).accessToken : null
  } catch {
    return null
  }
}

const FALLBACK_MESSAGES: Record<number, string> = {
  400: 'İstek geçersiz. Girdiğiniz bilgileri kontrol edin.',
  401: 'Oturumunuz sona erdi. Lütfen yeniden giriş yapın.',
  403: 'Bu işlem için yetkiniz yok.',
  404: 'İstenen kayıt bulunamadı.',
  502: 'Öğrenci bilgi sistemine (OBİS) şu anda ulaşılamıyor.',
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  // FormData için Content-Type'ı tarayıcı (sınır değeriyle birlikte) kendisi ayarlar.
  if (init.body !== undefined && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  const token = readStoredToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch {
    throw new ApiError(0, 'Sunucuya ulaşılamıyor. İnternet bağlantınızı ve sunucunun çalıştığını kontrol edin.')
  }

  if (response.ok) {
    return (response.status === 204 ? undefined : await response.json()) as T
  }

  let message = FALLBACK_MESSAGES[response.status]
  try {
    const body = (await response.json()) as { message?: string }
    if (body?.message) message = body.message
  } catch {
    // Gövde JSON değil; yedek mesaj kullanılır.
  }
  // Giriş denemesinin kendisi 401 döndürebilir; o durumda oturum yoktur, çıkış tetiklenmez.
  if (response.status === 401 && token) unauthorizedHandler?.()
  throw new ApiError(response.status, message ?? 'Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.')
}

/** Yetkili bir GET ile dosya indirir (CSV gibi); tarayıcıya kaydet iletişim kutusunu tetikler. */
async function download(path: string, fallbackName: string): Promise<void> {
  const headers = new Headers()
  const token = readStoredToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let response: Response
  try {
    response = await fetch(path, { headers })
  } catch {
    throw new ApiError(0, 'Sunucuya ulaşılamıyor. İnternet bağlantınızı ve sunucunun çalıştığını kontrol edin.')
  }
  if (!response.ok) {
    let message = FALLBACK_MESSAGES[response.status] ?? 'Dosya indirilemedi.'
    try {
      const body = (await response.json()) as { message?: string }
      if (body?.message) message = body.message
    } catch {
      // Gövde JSON değil; yedek mesaj kullanılır.
    }
    throw new ApiError(response.status, message)
  }

  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fallbackName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  postForm: <T>(path: string, form: FormData) => request<T>(path, { method: 'POST', body: form }),
  put: <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  download,
}
