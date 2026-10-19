import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'

interface Schema {
  $ref?: string
  type?: string
  format?: string
  enum?: unknown[]
  default?: unknown
  properties?: Record<string, Schema>
  items?: Schema
  allOf?: Schema[]
  readOnly?: boolean
}
export interface ApiParameter {
  name: string
  in: 'path' | 'query' | 'header'
  required?: boolean
  schema: Schema
}
interface OpenApi {
  paths: Record<string, Record<string, {
    tags?: string[]
    summary?: string
    parameters?: ApiParameter[]
    requestBody?: { content: Record<string, { schema: Schema }> }
  }>>
  components: { schemas: Record<string, Schema> }
}
export interface ApiOperation {
  key: string
  method: 'GET' | 'POST' | 'PUT' | 'DELETE'
  path: string
  group: string
  summary: string
  parameters: ApiParameter[]
  sampleBody?: unknown
  machineOnly: boolean
}

function example(schema: Schema, definitions: OpenApi['components']['schemas'], depth = 0): unknown {
  if (depth > 8) return null
  if (schema.$ref) return example(definitions[schema.$ref.split('/').pop()!] ?? {}, definitions, depth + 1)
  if (schema.default !== undefined) return schema.default
  if (schema.enum?.length) return schema.enum[0]
  if (schema.allOf) return Object.assign({}, ...schema.allOf.map((s) => example(s, definitions, depth + 1)))
  if (schema.properties) return Object.fromEntries(Object.entries(schema.properties)
    .filter(([, property]) => !property.readOnly)
    .map(([key, property]) => [key, example(property, definitions, depth + 1)]))
  if (schema.type === 'array') return [example(schema.items ?? {}, definitions, depth + 1)]
  if (schema.type === 'boolean') return false
  if (schema.type === 'integer' || schema.type === 'number') return 0
  if (schema.format === 'date-time') return new Date().toISOString()
  if (schema.format === 'uuid') return ''
  return ''
}

/** Danh sách lấy trực tiếp từ Swagger của backend đang chạy, không tự đoán route/DTO. */
export async function getApiOperations(): Promise<ApiOperation[]> {
  const schema = await apiRequest<OpenApi>(API_ENDPOINTS.openApi)
  return Object.entries(schema.paths).flatMap(([path, methods]) => Object.entries(methods)
    .filter(([method]) => ['get', 'post', 'put', 'delete'].includes(method))
    .map(([method, operation]): ApiOperation => {
      const bodySchema = operation.requestBody?.content['application/json']?.schema
      return {
        key: `${method.toUpperCase()} ${path}`, method: method.toUpperCase() as ApiOperation['method'], path,
        group: operation.tags?.[0] ?? 'API', summary: operation.summary ?? '',
        parameters: operation.parameters ?? [],
        sampleBody: bodySchema ? example(bodySchema, schema.components.schemas) : undefined,
        machineOnly: /\/payments\/vnpay-(ipn|return)$/i.test(path),
      }
    }))
}

export async function executeApi(operation: ApiOperation, values: Record<string, string>, body?: unknown): Promise<unknown> {
  if (operation.machineOnly) throw new Error('Endpoint callback chỉ được gọi bởi luồng thanh toán và gateway.')
  const path = operation.path.replace(/\{([^}]+)\}/g, (_, name: string) => {
    if (!values[name]?.trim()) throw new Error(`Thiếu ${name}`)
    return encodeURIComponent(values[name].trim())
  })
  const query: Record<string, string | string[]> = {}
  const headers: Record<string, string> = {}
  for (const parameter of operation.parameters) {
    const value = values[parameter.name]?.trim()
    if (parameter.required && !value) throw new Error(`Thiếu ${parameter.name}`)
    if (value && parameter.in === 'query') query[parameter.name] = parameter.schema.type === 'array'
      ? value.split(',').map((part) => part.trim()).filter(Boolean) : value
    if (value && parameter.in === 'header') headers[parameter.name] = value
  }
  return apiRequest(path, { method: operation.method, query, headers, body })
}
