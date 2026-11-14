import { useState } from 'react'
import type { ReactNode } from 'react'
import Pagination from './Pagination'
import { t } from '../../i18n'

export interface Column<T> {
  header: string
  render: (row: T) => ReactNode
  className?: string
}

/** Dense table for the admin lists. */
export default function DataTable<T>({
  columns,
  rows,
  rowKey,
  empty = 'Nothing here yet.',
  paginate = false,
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  empty?: string
  paginate?: boolean
}) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const identity = rows.map(rowKey).join(',')
  const [previousIdentity, setPreviousIdentity] = useState(identity)
  if (identity !== previousIdentity) {
    setPreviousIdentity(identity)
    setPage(1)
  }
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize))
  const currentPage = Math.min(page, totalPages)
  const visibleRows = paginate ? rows.slice((currentPage - 1) * pageSize, currentPage * pageSize) : rows
  return (
    <div>
    <div className="overflow-x-auto rounded-2xl border border-outline-variant/30 bg-white shadow-card">
      <table className="w-full min-w-[40rem] text-left text-sm">
        <thead className="border-b border-outline-variant/30 bg-mist text-2xs uppercase tracking-wider text-on-surface-variant">
          <tr>
            {columns.map((c) => (
              <th
                key={c.header}
                scope="col"
                className={`whitespace-nowrap px-4 py-3 font-semibold ${c.className ?? ''}`.trim()}
              >
                {t(c.header)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-outline-variant/30">
          {rows.length === 0 ? (
            <tr>
              <td
                colSpan={columns.length}
                className="px-4 py-8 text-center text-on-surface-variant"
              >
                {t(empty)}
              </td>
            </tr>
          ) : (
            visibleRows.map((row) => (
              <tr
                key={rowKey(row)}
                className="transition-colors hover:bg-teal-flow/[0.035] focus-within:bg-teal-flow/[0.035]"
              >
                {columns.map((c) => (
                  <td
                    key={c.header}
                    className={`px-4 py-3 text-deep-river first:whitespace-nowrap ${
                      c.className ?? ''
                    }`.trim()}
                  >
                    {c.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
    {paginate && rows.length > 0 && (
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-b-2xl border border-t-0 border-outline-variant/30 bg-white px-4 py-3">
        <span className="text-xs text-on-surface-variant" aria-live="polite">
          {t('Showing {from}–{to} of {total}', { from: (currentPage - 1) * pageSize + 1, to: Math.min(currentPage * pageSize, rows.length), total: rows.length })}
        </span>
        <label className="flex items-center gap-2 text-xs text-on-surface-variant">
          {t('Rows per page')}
          <select className="min-h-[44px] rounded-xl border-outline-variant/50 text-sm text-deep-river focus:ring-teal-flow" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1) }}>
            {[10, 20, 50].map((size) => <option key={size} value={size}>{size}</option>)}
          </select>
        </label>
        <Pagination pageNumber={currentPage} totalPages={totalPages} onChange={setPage} />
      </div>
    )}
    </div>
  )
}
