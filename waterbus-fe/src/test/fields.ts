import { fireEvent, screen } from '@testing-library/react'

/** Đặt giá trị cho một ô theo nhãn: mở danh sách thả xuống và chọn lựa chọn theo `value`, hoặc đổi giá trị ô nhập. */
export function change(label: string, value: string) {
  const field = screen.getByLabelText(label)
  if (field.getAttribute('aria-haspopup') === 'listbox') {
    fireEvent.click(field)
    const option = document.querySelector(`[role="option"][data-value="${value}"]`)
    if (!option) throw new Error(`No option with value "${value}" in "${label}"`)
    fireEvent.click(option)
    return
  }
  fireEvent.change(field, { target: { value } })
}

/** Mở danh sách của ô chọn, xác nhận có lựa chọn khớp `name`, rồi đóng lại. */
export async function hasOption(label: string, name: RegExp) {
  const button = await screen.findByLabelText(label)
  fireEvent.click(button)
  const option = await screen.findByRole('option', { name })
  fireEvent.click(button)
  return option
}

/** Mở danh sách của ô chọn, xác nhận KHÔNG có lựa chọn khớp `name`, rồi đóng lại. */
export async function lacksOption(label: string, name: RegExp) {
  const button = await screen.findByLabelText(label)
  fireEvent.click(button)
  const found = screen.queryByRole('option', { name })
  fireEvent.click(button)
  return found === null
}
