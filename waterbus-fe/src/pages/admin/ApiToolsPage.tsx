import { useState } from 'react'
import type { FormEvent } from 'react'
import { useDocumentTitle, useFetch } from '../../hooks'
import { Button, ErrorState, Icon, PageHeader, PageLoader, Panel } from '../../components/ui'
import { ApiError } from '../../services/apiClient'
import { executeApi, getApiOperations } from '../../features/admin/services/apiToolsService'

/** Công cụ test local cho các API chưa có màn hình nghiệp vụ riêng. */
export default function ApiToolsPage() {
  useDocumentTitle('Test API')
  const { data, loading, error, retry } = useFetch(getApiOperations)
  const [key, setKey] = useState('')
  const [values, setValues] = useState<Record<string, string>>({})
  const [body, setBody] = useState('')
  const [result, setResult] = useState('')
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const operation = data?.find((item) => item.key === key) ?? data?.[0]

  async function send(event: FormEvent) {
    event.preventDefault()
    if (!operation || busy) return
    setBusy(true)
    setResult('')
    setFailed(false)
    try {
      const response = await executeApi(
        operation,
        values,
        operation.sampleBody === undefined
          ? undefined
          : JSON.parse(body || JSON.stringify(operation.sampleBody)),
      )
      setResult(
        response === undefined ? 'Thành công (204 No Content)' : JSON.stringify(response, null, 2),
      )
    } catch (error) {
      setFailed(true)
      setResult(
        error instanceof ApiError
          ? JSON.stringify(
              {
                status: error.status,
                message: error.message,
                errors: error.fieldErrors,
                traceId: error.traceId,
              },
              null,
              2,
            )
          : error instanceof Error
          ? error.message
          : 'Không thực hiện được yêu cầu.',
      )
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <PageLoader />
  if (error || !data || !operation) return <ErrorState onRetry={retry} />
  const inputClass =
    'min-h-[44px] w-full rounded-xl border border-outline-variant/60 bg-white px-3 py-2.5 text-sm text-deep-river transition-colors focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15'
  const methodClass =
    operation.method === 'GET'
      ? 'bg-teal-flow/10 text-teal-flow'
      : operation.method === 'DELETE'
      ? 'bg-red-50 text-red-700'
      : 'bg-amber-50 text-amber-800'
  return (
    <div>
      <PageHeader
        title="Test API backend"
        description="Chọn endpoint, nhập dữ liệu và xem phản hồi từ máy chủ."
        actions={
          <span className="rounded-full border border-teal-flow/15 bg-teal-flow/5 px-3 py-1.5 text-xs font-semibold text-teal-flow">
            {data.length} endpoints
          </span>
        }
      />
      <div className="grid min-w-0 items-start gap-5 xl:grid-cols-2">
        <Panel>
          <div className="mb-5 flex items-center gap-3 border-b border-outline-variant/30 pb-4">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-teal-flow/10 text-teal-flow">
              <Icon name="terminal" className="text-[20px]" />
            </span>
            <h2 className="font-semibold text-deep-river">Yêu cầu API</h2>
          </div>
          <form className="space-y-4" onSubmit={send}>
            <fieldset disabled={busy} className="min-w-0 space-y-4 disabled:opacity-70">
              <label className="block space-y-2 text-xs font-semibold text-on-surface-variant">
                Endpoint
                <select
                  className={inputClass}
                  value={operation.key}
                  onChange={(event) => {
                    const next = data.find((item) => item.key === event.target.value)!
                    setKey(next.key)
                    setValues({})
                    setResult('')
                    setFailed(false)
                    setBody(
                      next.sampleBody === undefined ? '' : JSON.stringify(next.sampleBody, null, 2),
                    )
                  }}
                >
                  {[...new Set(data.map((item) => item.group))].map((group) => (
                    <optgroup key={group} label={group}>
                      {data
                        .filter((item) => item.group === group)
                        .map((item) => (
                          <option key={item.key} value={item.key}>
                            {item.key}
                          </option>
                        ))}
                    </optgroup>
                  ))}
                </select>
              </label>
              <div className="flex min-w-0 items-start gap-2 rounded-xl bg-mist p-3">
                <span
                  className={`shrink-0 rounded-md px-2 py-1 font-mono text-[11px] font-bold ${methodClass}`}
                >
                  {operation.method}
                </span>
                <code className="min-w-0 break-all pt-1 text-xs leading-relaxed text-deep-river">
                  {operation.path}
                </code>
              </div>
              {operation.summary && (
                <p className="text-sm leading-relaxed text-on-surface-variant">
                  {operation.summary}
                </p>
              )}
              <div className="grid gap-3 md:grid-cols-2">
                {operation.parameters.map((parameter) => (
                  <label
                    className="min-w-0 space-y-1.5 text-xs font-semibold text-on-surface-variant"
                    key={`${parameter.in}-${parameter.name}`}
                  >
                    {parameter.name} ({parameter.in}) {parameter.required ? '*' : ''}
                    <input
                      className={inputClass}
                      required={parameter.required}
                      autoComplete="off"
                      type={parameter.in === 'header' ? 'password' : 'text'}
                      value={values[parameter.name] ?? ''}
                      placeholder={parameter.schema.format ?? parameter.schema.type}
                      onChange={(event) =>
                        setValues({ ...values, [parameter.name]: event.target.value })
                      }
                    />
                  </label>
                ))}
              </div>
              {operation.sampleBody !== undefined && (
                <label className="block space-y-2 text-xs font-semibold text-on-surface-variant">
                  Request JSON
                  <textarea
                    className={`${inputClass} resize-y font-mono text-xs leading-relaxed`}
                    rows={12}
                    spellCheck={false}
                    value={body || JSON.stringify(operation.sampleBody, null, 2)}
                    onChange={(event) => setBody(event.target.value)}
                  />
                </label>
              )}
              <Button size="sm"
                type="submit"
                icon={busy ? 'hourglass_top' : 'send'}
                className="w-full sm:w-auto"
                disabled={busy || operation.machineOnly}
              >
                {busy
                  ? 'Đang gọi API…'
                  : operation.machineOnly
                  ? 'Callback do gateway gọi'
                  : 'Gửi yêu cầu'}
              </Button>
            </fieldset>
          </form>
          <details className="mt-5 border-t border-outline-variant/30 pt-4 text-xs text-on-surface-variant">
            <summary className="cursor-pointer font-semibold text-teal-flow">
              Hướng dẫn kiểm thử
            </summary>
            <p className="mt-2 leading-relaxed">
              Dùng phiên đăng nhập hiện tại; backend kiểm tra quyền từng yêu cầu. Nhập GUID thật từ
              kết quả GET. Endpoint Staff được test trên app mobile; callback VNPay do gateway gọi.
              Audio chưa có endpoint HTTP.
            </p>
          </details>
        </Panel>
        <section
          className="min-w-0 overflow-hidden rounded-2xl border border-outline-variant/30 bg-white shadow-sm xl:sticky xl:top-6"
          aria-busy={busy}
        >
          <div className="flex items-center justify-between gap-3 border-b border-outline-variant/30 px-5 py-4">
            <h2 className="text-sm font-semibold text-deep-river">Phản hồi từ máy chủ</h2>
            <span
              role="status"
              className={`rounded-full px-2.5 py-1 text-[11px] font-semibold ${
                result
                  ? failed
                    ? 'bg-red-50 text-red-700'
                    : 'bg-teal-flow/10 text-teal-flow'
                  : 'bg-mist text-on-surface-variant'
              }`}
            >
              {busy ? 'Đang gửi…' : result ? (failed ? 'Có lỗi' : 'Thành công') : 'Chưa gửi'}
            </span>
          </div>
          {result ? (
            <pre
              tabIndex={0}
              aria-label="Nội dung phản hồi"
              className="max-h-[600px] min-h-[280px] overflow-auto whitespace-pre-wrap break-words bg-deep-river p-5 font-mono text-xs leading-relaxed text-slate-100"
            >
              {result}
            </pre>
          ) : (
            <div className="flex min-h-[300px] flex-col items-center justify-center gap-3 px-6 py-12 text-center">
              <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-mist text-teal-flow">
                <Icon name="data_object" className="text-[28px]" />
              </span>
              <p className="text-sm font-semibold text-deep-river">
                {busy ? 'Đang chờ máy chủ' : 'Sẵn sàng kiểm thử'}
              </p>
              <p className="max-w-[260px] text-xs leading-relaxed text-on-surface-variant">
                Kết quả sẽ hiển thị tại đây sau khi bạn gửi yêu cầu.
              </p>
            </div>
          )}
        </section>
      </div>
    </div>
  )
}
