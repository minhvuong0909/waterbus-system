import { apiRequest } from '../../../services/apiClient'

/** `POST {item}/activate` hoặc `{item}/deactivate`: mục danh mục chỉ bị tắt, không bao giờ bị xoá. */
export function setActive<T = void>(itemPath: string, active: boolean): Promise<T> {
  return apiRequest<T>(`${itemPath}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' })
}
