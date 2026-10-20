import { API_BASE_URL } from '../constant/env'
import { getLocale } from '../i18n/translate'
import { getToken } from './authStorage'

/** Error raised for any failed request. `status` is 0 when the server could not be reached. */
export class ApiError extends Error {
  readonly status: number
  /** Per-field validation messages from a 400 response, keyed by property name. */
  readonly fieldErrors: Record<string, string[]>
  readonly traceId?: string

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
    this.traceId = traceId
  }
}

/** A request that has not answered after this long is given up, so the UI never waits forever. */
const REQUEST_TIMEOUT_MS = 30_000

type QueryValue = string | number | boolean | readonly (string | number | boolean)[] | null | undefined

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  /** Extra headers, e.g. the guest token of an order. */
  headers?: Record<string, string>
  query?: Record<string, QueryValue>
  body?: unknown
  signal?: AbortSignal
}

// Called when a request that carried a token comes back 401, so the app can sign the user out.
let onUnauthorized: (() => void) | undefined

export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const params = new URLSearchParams()
  Object.entries(query ?? {}).forEach(([key, value]) => {
    if (Array.isArray(value)) value.forEach((item) => params.append(key, String(item)))
    else if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  })
  const search = params.toString()
  return `${API_BASE_URL}${path}${search ? `?${search}` : ''}`
}

interface ErrorBody {
  traceId?: string
  /** ProblemDetails (RFC 7807) from GlobalExceptionHandlingMiddleware. */
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  /** Shape used by AuthController.Login for a failed sign-in. */
  message?: string
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: ErrorBody = {}
  try {
    body = (await response.json()) as ErrorBody
  } catch {
    // Not a JSON body (e.g. a proxy error page): fall back to the status text.
  }
  const message = body.message ?? body.detail ?? body.title ?? response.statusText
  return new ApiError(response.status, message, body.errors, body.traceId)
}

/** JSON request to the backend. Adds the bearer token and `Accept-Language`, and throws `ApiError`. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', query, body, signal, headers: extraHeaders } = options
  const token = getToken()

  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Accept-Language': getLocale(),
    ...extraHeaders,
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS)
  const forward = () => controller.abort()
  if (signal?.aborted) controller.abort()
  else signal?.addEventListener('abort', forward, { once: true })
  let response: Response
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    })
  } catch (error) {
    // Aborted by the caller (e.g. an unmounted page): not an error to show.
    if (signal?.aborted) throw error
    throw new ApiError(0, 'Unable to reach the server.')
  } finally {
    clearTimeout(timer)
    signal?.removeEventListener('abort', forward)
  }

  if (!response.ok) {
    if (response.status === 401 && token) onUnauthorized?.()
    throw await toApiError(response)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
