import type { Ticket } from '../../types/scanner';

/**
 * Chọn vé khi soát. Chỉ lưu các vé Staff BỎ chọn; vé được chọn luôn = vé còn đủ điều kiện (Valid + NotCheckedIn)
 * trừ đi vé bị bỏ chọn. Nhờ vậy khi trạng thái vé đổi (vừa check-in) lựa chọn tự đồng bộ, không giữ vé đã lên tàu.
 */
export const eligibleTicketIds = (tickets: Ticket[]): string[] =>
  tickets.filter((t) => t.status === 'Valid' && t.boardingStatus === 'NotCheckedIn').map((t) => t.id);

export const selectedTicketIds = (tickets: Ticket[], excluded: string[]): string[] =>
  eligibleTicketIds(tickets).filter((id) => !excluded.includes(id));

export const toggleTicket = (tickets: Ticket[], excluded: string[], ticketId: string): string[] => {
  if (!eligibleTicketIds(tickets).includes(ticketId)) return excluded;
  return excluded.includes(ticketId) ? excluded.filter((id) => id !== ticketId) : [...excluded, ticketId];
};

/** Đang chọn hết ⇒ bỏ chọn hết; ngược lại ⇒ chọn hết vé đủ điều kiện. */
export const toggleAllTickets = (tickets: Ticket[], excluded: string[]): string[] => {
  const eligible = eligibleTicketIds(tickets);
  return selectedTicketIds(tickets, excluded).length === eligible.length ? eligible : [];
};
