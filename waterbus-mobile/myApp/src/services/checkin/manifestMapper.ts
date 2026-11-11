/**
 * Chuyển ScannerManifestResponse (API thật) + QrPublicKeyResponse sang PreloadManifest
 * (shape nội bộ mobile đã dùng cho SQLite) - không cần đổi schema SQLite/sqliteRepository.
 */
import { ScannerManifestResponse, QrPublicKeyResponse } from '../../types/checkin';
import { PreloadManifest, Trip, Booking, Ticket, PublicKeyInfo } from '../../types/scanner';

export function mapScannerManifestToPreloadManifest(
  response: ScannerManifestResponse,
  publicKey: QrPublicKeyResponse
): PreloadManifest {
  const trip: Trip = {
    id: response.trip.tripId,
    tripCode: response.trip.routeName,
    routeName: response.trip.routeName,
    tripType: response.trip.tripType as Trip['tripType'],
    boatName: response.trip.boatName,
    // Backend không trả Guid của 2 bến trong manifest này (chỉ trả tên để hiển thị);
    // không dùng để tra cứu nên để trống là an toàn.
    departureStationId: '',
    departureStationName: response.trip.departureStationName,
    arrivalStationId: '',
    arrivalStationName: response.trip.arrivalStationName,
    departureTime: response.trip.departureTime,
    arrivalTime: response.trip.arrivalTime ?? undefined,
    salesCloseAt: response.trip.salesCloseAt ?? undefined,
    status: response.trip.status,
  };

  const bookings: Booking[] = response.bookings.map((b) => {
    const tickets: Ticket[] = b.tickets.map((t) => ({
      id: t.ticketId,
      bookingId: b.bookingId,
      ticketNumber: t.ticketCode,
      seatCode: t.seatCode,
      seatClass: t.seatClass,
      passengerName: t.passengerName,
      // Manifest chỉ trả Ticket.Status == Valid (đã lọc ở backend) nên gán cố định ở đây.
      status: 'Valid',
      boardingStatus: t.boardingStatus as Ticket['boardingStatus'],
      faceFare: t.paidFare,
      paidFare: t.paidFare,
    }));

    return {
      id: b.bookingId,
      publicBookingId: b.publicBookingId,
      bookingCode: b.bookingCode,
      tripId: b.tripId,
      customerName: b.customerName,
      customerPhone: b.customerPhone ?? undefined,
      totalAmount: b.quotedTotal,
      qrCredentialVersion: b.qrCredentialVersion,
      status: b.status,
      tickets,
      trip,
    };
  });

  const publicKeyInfo: PublicKeyInfo = {
    kid: publicKey.kid,
    algorithm: publicKey.algorithm as PublicKeyInfo['algorithm'],
    publicKeyPem: publicKey.publicKeyPem,
    // Backend không trả thời hạn hiệu lực của key (chỉ RS256 hiện hành); đặt mặc định rộng,
    // verifier không dùng các field này để quyết định hợp lệ (chỉ dùng publicKeyPem + algorithm).
    validFrom: new Date().toISOString(),
    validTo: new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(),
    isActive: true,
  };

  return {
    stationId: response.tripStopCallId,
    stationName: response.stationName,
    generatedAt: response.generatedAt,
    publicKey: publicKeyInfo,
    trips: [trip],
    bookings,
  };
}
