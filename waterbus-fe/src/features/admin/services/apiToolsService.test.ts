import { afterEach, describe, expect, it, vi } from 'vitest'
import { executeApi, getApiOperations } from './apiToolsService'
import type { ApiOperation } from './apiToolsService'

const fetchMock = vi.fn()
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })
const operation: ApiOperation = { key: 'GET test', method: 'GET', path: '/api/v1/Stations/{id}',
  group: 'Stations', summary: '', machineOnly: false,
  parameters: [{ name: 'id', in: 'path', required: true, schema: { type: 'string' } },
    { name: 'pageNumber', in: 'query', schema: { type: 'integer' } }] }

describe('API tools contract integration', () => {
  it('loads endpoint templates and nested request examples from the running backend contract', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ paths: {
      '/api/v1/CheckIns/sync': { post: { requestBody: { content: { 'application/json': {
        schema: { $ref: '#/components/schemas/Sync' },
      } } } } },
    }, components: { schemas: { Sync: { properties: { events: { type: 'array', items: {
      properties: { clientEventId: { type: 'string' } },
    } } } } } } })))
    expect((await getApiOperations())[0]).toMatchObject({
      path: '/api/v1/CheckIns/sync', method: 'POST', sampleBody: { events: [{ clientEventId: '' }] },
    })
  })
  it('encodes path parameters and passes query values through the shared client', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ id: 'abc' })))
    await executeApi(operation, { id: 'abc/def', pageNumber: '2' })
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/v1\/Stations\/abc%2Fdef\?pageNumber=2$/)
  })
  it('rejects missing required path identifiers without sending a request', async () => {
    await expect(executeApi(operation, {})).rejects.toThrow('Thiếu id')
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('prevents manually sending gateway callbacks', async () => {
    await expect(executeApi({ ...operation, machineOnly: true }, { id: 'test' })).rejects.toThrow('gateway')
  })
})
