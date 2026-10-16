/**
 * Booking Detail & Passenger Check-In UI Modal
 * Displays booking details, verified digital signature badge,
 * passenger seat list, and per-passenger check-in checkboxes (BR-QR-03).
 */

import React, { useState, useEffect } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  SafeAreaView,
  Alert,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { Ionicons } from '@expo/vector-icons';
import { useScannerStore } from '../../store/useScannerStore';
import { Ticket } from '../../types/scanner';

export default function BookingDetailModal() {
  const {
    currentBooking,
    verificationResult,
    isModalOpen,
    closeBookingModal,
    performCheckIn,
    deviceInfo,
  } = useScannerStore();

  const [selectedTicketIds, setSelectedTicketIds] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Initialize selected tickets: auto-select unchecked tickets by default
  useEffect(() => {
    if (currentBooking) {
      const uncheckedIds = currentBooking.tickets
        .filter((t) => t.boardingStatus === 'NotCheckedIn')
        .map((t) => t.id);
      setSelectedTicketIds(uncheckedIds);
    } else {
      setSelectedTicketIds([]);
    }
  }, [currentBooking]);

  if (!currentBooking) return null;

  const toggleSelectTicket = (ticket: Ticket) => {
    if (ticket.boardingStatus === 'CheckedIn') return; // Cannot un-check checked ticket here

    if (selectedTicketIds.includes(ticket.id)) {
      setSelectedTicketIds(selectedTicketIds.filter((id) => id !== ticket.id));
    } else {
      setSelectedTicketIds([...selectedTicketIds, ticket.id]);
    }
  };

  const handleSelectAll = () => {
    const uncheckedIds = currentBooking.tickets
      .filter((t) => t.boardingStatus === 'NotCheckedIn')
      .map((t) => t.id);

    if (selectedTicketIds.length === uncheckedIds.length) {
      setSelectedTicketIds([]);
    } else {
      setSelectedTicketIds(uncheckedIds);
    }
  };

  const handleConfirmCheckIn = async () => {
    if (selectedTicketIds.length === 0) {
      Alert.alert('Chưa chọn vé', 'Vui lòng tích chọn ít nhất một hành khách đang có mặt để check-in.');
      return;
    }

    try {
      setIsSubmitting(true);
      await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);

      const result = await performCheckIn(selectedTicketIds);
      setIsSubmitting(false);

      if (result.success) {
        await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
        Alert.alert(
          'Check-in Thành Công',
          `Đã ghi nhận check-in cho ${result.count} hành khách vào bộ nhớ SQLite cục bộ.\nSự kiện được lưu vào hàng đợi đồng bộ an toàn (Idempotent Sync).`,
          [{ text: 'Đóng / Quét tiếp', onPress: closeBookingModal }]
        );
      } else {
        await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
        Alert.alert('Lỗi Check-in', result.error || 'Không thể thực hiện check-in');
      }
    } catch (e: any) {
      setIsSubmitting(false);
      Alert.alert('Lỗi', e.message);
    }
  };

  const uncheckedCount = currentBooking.tickets.filter((t) => t.boardingStatus === 'NotCheckedIn').length;
  const checkedCount = currentBooking.tickets.filter((t) => t.boardingStatus === 'CheckedIn').length;

  return (
    <Modal visible={isModalOpen} animationType="slide" transparent={false} onRequestClose={closeBookingModal}>
      <SafeAreaView style={styles.container}>
        {/* Header Bar */}
        <View style={styles.header}>
          <TouchableOpacity onPress={closeBookingModal} style={styles.closeButton}>
            <Ionicons name="close" size={28} color="#0F172A" />
          </TouchableOpacity>
          <View style={styles.headerTitleContainer}>
            <Text style={styles.headerTitle}>Chi Tiết Booking & Soát Vé</Text>
            <Text style={styles.headerSubtitle}>{currentBooking.bookingCode}</Text>
          </View>
          <View style={{ width: 40 }} />
        </View>

        <ScrollView contentContainerStyle={styles.scrollContent}>
          {/* Digital Signature Verification Badge */}
          {verificationResult?.isValid ? (
            <View style={styles.verifiedBanner}>
              <Ionicons name="shield-checkmark" size={24} color="#059669" />
              <View style={styles.verifiedTextContainer}>
                <Text style={styles.verifiedTitle}>CHỮ KÝ SỐ HỢP LỆ (XÁC THỰC OFFLINE)</Text>
                <Text style={styles.verifiedSubtitle}>
                  Thuật toán: {verificationResult.algorithmUsed || 'HS256'} • Public Key: OK • Mã toàn vẹn
                </Text>
              </View>
            </View>
          ) : (
            <View style={styles.invalidBanner}>
              <Ionicons name="alert-circle" size={24} color="#DC2626" />
              <View style={styles.verifiedTextContainer}>
                <Text style={styles.invalidTitle}>CẢNH BÁO: CHỮ KÝ SỐ KHÔNG HỢP LỆ</Text>
                <Text style={styles.invalidSubtitle}>{verificationResult?.error || 'QR không xác thực'}</Text>
              </View>
            </View>
          )}

          {/* Booking & Trip Summary Card */}
          <View style={styles.card}>
            <View style={styles.cardRow}>
              <Text style={styles.cardLabel}>Khách hàng:</Text>
              <Text style={styles.cardValueBold}>{currentBooking.customerName}</Text>
            </View>
            {currentBooking.customerPhone ? (
              <View style={styles.cardRow}>
                <Text style={styles.cardLabel}>Số điện thoại:</Text>
                <Text style={styles.cardValue}>{currentBooking.customerPhone}</Text>
              </View>
            ) : null}
            <View style={styles.divider} />
            <View style={styles.cardRow}>
              <Text style={styles.cardLabel}>Chuyến tàu:</Text>
              <Text style={styles.cardValueHighlight}>
                {currentBooking.trip?.routeName || 'Tuyến Bạch Đằng - Linh Đông'}
              </Text>
            </View>
            <View style={styles.cardRow}>
              <Text style={styles.cardLabel}>Phương tiện:</Text>
              <Text style={styles.cardValue}>{currentBooking.trip?.boatName || 'Tàu Sài Gòn 01'}</Text>
            </View>
            <View style={styles.cardRow}>
              <Text style={styles.cardLabel}>Bến kiểm soát:</Text>
              <Text style={styles.cardValue}>{deviceInfo.stationName}</Text>
            </View>
          </View>

          {/* Passenger Section Header */}
          <View style={styles.sectionHeader}>
            <View>
              <Text style={styles.sectionTitle}>
                Danh Sách Vé Trong Đơn ({currentBooking.tickets.length} khách)
              </Text>
              <Text style={styles.sectionSubtitle}>
                Đã lên: {checkedCount} • Chưa check-in: {uncheckedCount}
              </Text>
            </View>

            {uncheckedCount > 0 ? (
              <TouchableOpacity onPress={handleSelectAll} style={styles.selectAllButton}>
                <Text style={styles.selectAllText}>
                  {selectedTicketIds.length === uncheckedCount ? 'Bỏ chọn' : 'Chọn tất cả'}
                </Text>
              </TouchableOpacity>
            ) : null}
          </View>

          {/* List of Tickets / Passengers */}
          {currentBooking.tickets.map((ticket, index) => {
            const isCheckedIn = ticket.boardingStatus === 'CheckedIn';
            const isSelected = selectedTicketIds.includes(ticket.id);

            return (
              <TouchableOpacity
                key={ticket.id || index}
                style={[
                  styles.ticketItem,
                  isCheckedIn && styles.ticketItemCheckedIn,
                  isSelected && styles.ticketItemSelected,
                ]}
                onPress={() => toggleSelectTicket(ticket)}
                activeOpacity={isCheckedIn ? 1 : 0.7}
              >
                {/* Checkbox */}
                <View style={styles.checkboxContainer}>
                  {isCheckedIn ? (
                    <Ionicons name="checkmark-done-circle" size={26} color="#059669" />
                  ) : isSelected ? (
                    <Ionicons name="checkbox" size={26} color="#0284C7" />
                  ) : (
                    <Ionicons name="square-outline" size={26} color="#94A3B8" />
                  )}
                </View>

                {/* Ticket Details */}
                <View style={styles.ticketDetails}>
                  <View style={styles.ticketRowTop}>
                    <Text style={styles.passengerName}>{ticket.passengerName}</Text>
                    <View
                      style={[
                        styles.statusBadge,
                        isCheckedIn ? styles.badgeGreen : styles.badgeAmber,
                      ]}
                    >
                      <Text
                        style={[
                          styles.statusBadgeText,
                          isCheckedIn ? styles.textGreen : styles.textAmber,
                        ]}
                      >
                        {isCheckedIn ? 'ĐÃ LÊN TÀU' : 'CHƯA CHECK-IN'}
                      </Text>
                    </View>
                  </View>

                  <View style={styles.ticketRowBottom}>
                    <View style={styles.seatBadge}>
                      <Text style={styles.seatBadgeText}>Ghế: {ticket.seatCode}</Text>
                    </View>
                    <Text style={styles.seatClassText}>{ticket.seatClass}</Text>
                    <Text style={styles.ticketNumberText}>{ticket.ticketNumber}</Text>
                  </View>
                </View>
              </TouchableOpacity>
            );
          })}
        </ScrollView>

        {/* Bottom Action Footer */}
        <View style={styles.footer}>
          <TouchableOpacity
            style={[
              styles.checkInButton,
              (selectedTicketIds.length === 0 || isSubmitting) && styles.buttonDisabled,
            ]}
            onPress={handleConfirmCheckIn}
            disabled={selectedTicketIds.length === 0 || isSubmitting}
          >
            <Ionicons name="checkmark-circle" size={22} color="#FFFFFF" style={{ marginRight: 8 }} />
            <Text style={styles.checkInButtonText}>
              {isSubmitting
                ? 'ĐANG GHI NHẬN...'
                : `XÁC NHẬN CHECK-IN (${selectedTicketIds.length} VÉ)`}
            </Text>
          </TouchableOpacity>
        </View>
      </SafeAreaView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F8FAFC',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#E2E8F0',
    backgroundColor: '#FFFFFF',
  },
  closeButton: {
    padding: 6,
  },
  headerTitleContainer: {
    alignItems: 'center',
  },
  headerTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#0F172A',
  },
  headerSubtitle: {
    fontSize: 13,
    color: '#64748B',
    marginTop: 2,
  },
  scrollContent: {
    padding: 16,
    paddingBottom: 24,
  },
  verifiedBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#ECFDF5',
    borderWidth: 1,
    borderColor: '#A7F3D0',
    borderRadius: 10,
    padding: 12,
    marginBottom: 16,
  },
  verifiedTextContainer: {
    marginLeft: 10,
    flex: 1,
  },
  verifiedTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#065F46',
  },
  verifiedSubtitle: {
    fontSize: 11,
    color: '#047857',
    marginTop: 2,
  },
  invalidBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FEF2F2',
    borderWidth: 1,
    borderColor: '#FECACA',
    borderRadius: 10,
    padding: 12,
    marginBottom: 16,
  },
  invalidTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#991B1B',
  },
  invalidSubtitle: {
    fontSize: 11,
    color: '#B91C1C',
    marginTop: 2,
  },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 16,
    marginBottom: 20,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  cardRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginVertical: 4,
  },
  cardLabel: {
    fontSize: 14,
    color: '#64748B',
  },
  cardValue: {
    fontSize: 14,
    color: '#1E293B',
    fontWeight: '500',
  },
  cardValueBold: {
    fontSize: 15,
    color: '#0F172A',
    fontWeight: '700',
  },
  cardValueHighlight: {
    fontSize: 14,
    color: '#0284C7',
    fontWeight: '600',
  },
  divider: {
    height: 1,
    backgroundColor: '#F1F5F9',
    marginVertical: 8,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-end',
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: '#0F172A',
  },
  sectionSubtitle: {
    fontSize: 12,
    color: '#64748B',
    marginTop: 2,
  },
  selectAllButton: {
    paddingVertical: 4,
    paddingHorizontal: 10,
    backgroundColor: '#E0F2FE',
    borderRadius: 6,
  },
  selectAllText: {
    fontSize: 13,
    color: '#0284C7',
    fontWeight: '600',
  },
  ticketItem: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    borderRadius: 10,
    padding: 14,
    marginBottom: 10,
    borderWidth: 1.5,
    borderColor: '#E2E8F0',
  },
  ticketItemSelected: {
    borderColor: '#0284C7',
    backgroundColor: '#F0F9FF',
  },
  ticketItemCheckedIn: {
    borderColor: '#D1FAE5',
    backgroundColor: '#F0FDF4',
    opacity: 0.85,
  },
  checkboxContainer: {
    marginRight: 12,
  },
  ticketDetails: {
    flex: 1,
  },
  ticketRowTop: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  passengerName: {
    fontSize: 15,
    fontWeight: '700',
    color: '#0F172A',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  badgeGreen: {
    backgroundColor: '#DCFCE7',
  },
  badgeAmber: {
    backgroundColor: '#FEF3C7',
  },
  statusBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  textGreen: {
    color: '#166534',
  },
  textAmber: {
    color: '#B45309',
  },
  ticketRowBottom: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  seatBadge: {
    backgroundColor: '#0F172A',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  seatBadgeText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },
  seatClassText: {
    fontSize: 12,
    color: '#64748B',
  },
  ticketNumberText: {
    fontSize: 11,
    color: '#94A3B8',
    marginLeft: 'auto',
  },
  footer: {
    backgroundColor: '#FFFFFF',
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: '#E2E8F0',
  },
  checkInButton: {
    backgroundColor: '#0284C7',
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 14,
    borderRadius: 10,
    shadowColor: '#0284C7',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.3,
    shadowRadius: 5,
    elevation: 3,
  },
  buttonDisabled: {
    backgroundColor: '#94A3B8',
    shadowOpacity: 0,
    elevation: 0,
  },
  checkInButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
});

