export function getTripTypeLabel(tripType: string): string {
  switch (tripType) {
    case 'Commuter':
      return 'Tàu buýt';
    case 'Sightseeing':
      return 'Du lịch ngắm cảnh';
    default:
      return 'Loại chuyến khác';
  }
}

export function getTripStatusLabel(status: string): string {
  const labels: Record<string, string> = {
    Scheduled: 'Sắp khởi hành',
    Boarding: 'Đang đón khách',
    EnRoute: 'Đang di chuyển',
    Completed: 'Đã hoàn thành',
    Cancelled: 'Đã hủy',
    Delayed: 'Bị trễ',
  };

  return labels[status] ?? 'Chưa xác định';
}

export function getSeatClassLabel(seatClass: string): string {
  const normalized = seatClass.toLocaleLowerCase('vi-VN');

  if (normalized.includes('frontcabin') || normalized.includes('khoang trước')) {
    return 'Khoang phía trước';
  }
  if (normalized.includes('standard') || normalized.includes('tiêu chuẩn')) {
    return 'Ghế tiêu chuẩn';
  }
  if (normalized.includes('outdoor') || normalized.includes('ngoài trời')) {
    return 'Boong ngoài trời';
  }

  return 'Hạng ghế khác';
}
