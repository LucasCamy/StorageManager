type Problem = { title?: string; detail?: string; code?: string; errors?: Record<string, string[]> }
export class ApiError extends Error {
  constructor(public status: number, public problem: Problem) { super(problem.detail || problem.title || 'Não foi possível concluir a solicitação.'); this.name = 'ApiError' }
}
let csrf: Promise<string> | undefined
export function resetCsrf() { csrf = undefined }
async function csrfToken() {
  if (!csrf) csrf = fetch('/api/v1/auth/csrf', { credentials: 'same-origin' }).then(async response => {
    if (!response.ok) throw new Error('Não foi possível estabelecer uma sessão segura. Tente novamente.')
    return (await response.json() as { token: string }).token
  }).catch(error => { csrf = undefined; throw error })
  return csrf
}
export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.method && !['GET', 'HEAD'].includes(init.method.toUpperCase())) {
    headers.set('Content-Type', 'application/json')
    headers.set('X-CSRF-TOKEN', await csrfToken())
  }
  let response: Response
  try { response = await fetch(`/api/v1${path}`, { ...init, headers, credentials: 'same-origin' }) }
  catch { throw new Error('Sem conexão com o servidor. Verifique sua rede e tente novamente.') }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({ title: `O servidor respondeu com erro ${response.status}.` })) as Problem
    if (response.status === 401 && path !== '/auth/login' && path !== '/me') window.dispatchEvent(new Event('session-expired'))
    if (response.status === 400 && /csrf|antiforgery/i.test(`${problem.code} ${problem.detail} ${problem.title}`)) resetCsrf()
    throw new ApiError(response.status, problem)
  }
  if (response.status === 204) return undefined as T
  const content = await response.text()
  return content ? JSON.parse(content) as T : undefined as T
}
export function params(values: Record<string, string | number | boolean | undefined>) {
  const search = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => { if (value !== undefined && value !== '') search.set(key, String(value)) })
  return `?${search.toString()}`
}
export function errorMessage(error: unknown) {
  if (error instanceof ApiError && error.problem.errors) return Object.values(error.problem.errors).flat().join(' ')
  return error instanceof Error ? error.message : 'Ocorreu um erro inesperado. Tente novamente.'
}
