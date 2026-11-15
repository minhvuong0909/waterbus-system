import { useState } from 'react'
import type { ReactNode } from 'react'
import Pagination from './Pagination'
import { SURFACE_CLASS } from './styles'
import { t } from '../../i18n'

export interface Column<T> {
  header: string
  render: (row: T) => ReactNode
  className?: string
}

const PAGE_SIZES = [10, 20, 50]

/** Bảng gọn cho các danh sách của khu điều hành; `paginate` chia trang ngay trên trình duyệt. */
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
  const [pageSize, setPageSize] = useState(PAGE_SIZES[0])
  const identity = rows.map(rowKey).join(',')
  const [previousIdentity, setPreviousIdentity] = useState(identity)
  if (identity !== previousIdentity) {
    setPreviousIdentity(identity)
    setPage(1)
  }
  const totalPages = Math.max(1, Math.ceil(rows.length / pageSize))
  const currentPage = Math.min(page, totalPages)
  const visibleRows = paginate ? rows.slice((currentPage - 1) * pageSize, currentPage * pageSize) : rows
  const showFooter = paginate && rows.length > PAGE_SIZES[0]

  return (
    <div className={`${SURFACE_CLASS} overflow-hidden`}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[40rem] text-left text-sm">
          <thead className="border-b border-outline-variant/30 bg-mist text-xs text-on-surface-variant">
            <tr>
              {columns.map((c) => (
                <th
                  key={c.header}
                  scope="col"
                  className={`whitespace-nowrap px-4 py-2.5 font-semibold ${c.className ?? ''}`.trim()}
                >
                  {t(c.header)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-outline-variant/30">
            {rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-8 text-center text-on-surface-variant">
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
                      className={`px-4 py-3 text-deep-river first:whitespace-nowrap ${c.className ?? ''}`.trim()}
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
      {showFooter && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-outline-variant/30 px-4 py-2.5">
          <span className="text-xs tabular-nums text-on-surface-variant">
            {t('{from}–{to} of {total}', {
              from: (currentPage - 1) * pageSize + 1,
              to: Math.min(currentPage * pageSize, rows.length),
              total: rows.length,
            })}
          </span>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2 text-xs text-on-surface-variant">
              {t('Per page')}
              <select
                className="min-h-[36px] rounded-xl border-outline-variant/70 py-1 pl-2 pr-7 text-sm text-deep-river focus:ring-teal-flow"
                value={pageSize}
                onChange={(event) => {
                  setPageSize(Number(event.target.value))
                  setPage(1)
                }}
              >
                {PAGE_SIZES.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </label>
            <Pagination pageNumber={currentPage} totalPages={totalPages} onChange={setPage} />
          </div>
        </div>
      )}
    </div>
  )
}
