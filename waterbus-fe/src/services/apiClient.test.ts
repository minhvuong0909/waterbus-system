import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiRequest, fileNameFromDisposition, setUnauthorizedHandler } from './apiClient'

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const fetchMock = vi.fn()

beforeEach(() => {
  window.localStorage.clear()
  window.sessionStorage.clear()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

describe('apiRequest', () => {
  it('sends array filters as repeated query parameters', async () => {
    fetchMock.mockResolvedValue(json([]))
    await apiRequest('/api/v1/trips/operations', { query: { statuses: ['Scheduled', 'Boarding'] } })
    expect(fetchMock.mock.calls[0][0]).toContain('statuses=Scheduled&statuses=Boarding')
  })
  it('builds the query string and skips empty values', async () => {
    fetchMock.mockResolvedValue(json({ ok: true }))
    await apiRequest('/api/v1/trips', { query: { page: 2, q: '', skip: undefined, vip: false } })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/api\/v1\/trips\?page=2&vip=false$/)
    expect(init.headers.Accept).toBe('application/json')
    expect(init.headers['Accept-Language']).toBeTruthy()
  })

  it('maps a ProblemDetails error and its field errors to ApiError', async () => {
    fetchMock.mockResolvedValue(
      json({ title: 'Bad', detail: 'Invalid input', errors: { email: ['Required'] } }, 400),
    )
    const error = await apiRequest('/x').catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).message).toBe('Invalid input')
    expect((error as ApiError).fieldErrors.email).toEqual(['Required'])
  })

  it('reports status 0 when the server cannot be reached', async () => {
    fetchMock.mockRejectedValue(new TypeError('network'))
    const error = await apiRequest('/x').catch((e: unknown) => e)
    expect((error as ApiError).status).toBe(0)
  })

  it('returns undefined for 204', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    await expect(apiRequest('/x', { method: 'DELETE' })).resolves.toBeUndefined()
  })

  it('signs out only on a 401 that carried a token', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(json({ title: 'Unauthorized' }, 401))

    await apiRequest('/x').catch(() => undefined)
    expect(handler).not.toHaveBeenCalled()

    const exp = Math.floor(Date.now() / 1000) + 3600
    const token = `h.${btoa(JSON.stringify({ exp }))}.s`
    window.sessionStorage.setItem('waterbus.session', JSON.stringify({ token }))
    await apiRequest('/x').catch(() => undefined)
    expect(handler).toHaveBeenCalledTimes(1)
  })
})

describe('apiRequest download', () => {
  it('returns the blob with the file name and row count the server sends', async () => {
    fetchMock.mockResolvedValue(
      new Response('a,b', {
        status: 200,
        headers: { 'Content-Disposition': 'attachment; filename=hoan-tien-20261112-1005.csv', 'X-Row-Count': '7' },
      }),
    )
    const file = await apiRequest<{ blob: Blob; fileName?: string; rowCount?: number }>('/api/v1/refunds/export', {
      method: 'POST',
      responseType: 'download',
    })
    expect(await file.blob.text()).toBe('a,b')
    expect(file.fileName).toBe('hoan-tien-20261112-1005.csv')
    expect(file.rowCount).toBe(7)
    expect((fetchMock.mock.calls[0][1] as RequestInit).headers).toMatchObject({ Accept: '*/*' })
  })

  it('leaves the details undefined when the server sends no headers', async () => {
    fetchMock.mockResolvedValue(new Response('x', { status: 200 }))
    const file = await apiRequest<{ fileName?: string; rowCount?: number }>('/x', { responseType: 'download' })
    expect(file.fileName).toBeUndefined()
    expect(file.rowCount).toBeUndefined()
  })

  it('still maps an error response to ApiError', async () => {
    fetchMock.mockResolvedValue(json({ title: 'Conflict', detail: 'Quá nhiều dòng' }, 409))
    await expect(apiRequest('/x', { method: 'POST', responseType: 'download' })).rejects.toMatchObject({ status: 409 })
  })
})

describe('fileNameFromDisposition', () => {
  it.each([
    ['attachment; filename=a.csv', 'a.csv'],
    ['attachment; filename="b c.csv"', 'b c.csv'],
    ["attachment; filename=a.csv; filename*=UTF-8''h%E1%BB%99.csv", 'a.csv'],
    ["attachment; filename*=UTF-8''h%E1%BB%99.csv", 'hộ.csv'],
  ])('reads %s', (header, expected) => expect(fileNameFromDisposition(header)).toBe(expected))

  it('returns undefined without a header', () => {
    expect(fileNameFromDisposition(null)).toBeUndefined()
    expect(fileNameFromDisposition('inline')).toBeUndefined()
  })
})
