/** Khớp validator của BE (`^\+?[0-9 ()-]{8,30}$`) để báo lỗi ngay trên form. */
export const isPhone = (value: string): boolean => /^\+?[0-9 ()-]{8,30}$/.test(value.trim())

/** Kiểm tra email đủ dùng cho form; BE vẫn là nơi quyết định. */
export const isEmail = (value: string): boolean => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())
