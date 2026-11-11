/** Lưu `blob` về máy dưới tên `fileName` (tạo liên kết tạm rồi bấm vào nó). */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  // Cho trình duyệt thời gian bắt đầu tải trước khi giải phóng URL.
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
