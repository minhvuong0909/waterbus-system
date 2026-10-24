import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  TextInput,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import * as Haptics from 'expo-haptics';
import { SqliteRepository } from '../src/services/sqlite/sqliteRepository';
import { QrVerifier } from '../src/services/crypto/qrVerifier';
import { useScannerStore } from '../src/store/useScannerStore';
import { Booking } from '../src/types/scanner';
import { BrandColors, Radius } from '../constants/theme';

export default function ManualInspectionModal() {
  const [code, setCode] = useState('');
  const [isSearching, setIsSearching] = useState(false);
  const [foundBooking, setFoundBooking] = useState<Booking | null>(null);
  const { setCurrentBooking } = useScannerStore();

  const handleSearch = async (searchTerm?: string) => {
    const query = (searchTerm || code).trim();
    if (!query) {
      Alert.alert('Thông báo', 'Vui lòng nhập mã vé hoặc mã Booking.');
      return;
    }

    try {
      setIsSearching(true);
      await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);

      let booking = await SqliteRepository.getBookingByPublicId(query);
      if (!booking) {
        booking = await SqliteRepository.searchBookingOrTicket(query);
      }

      setIsSearching(false);

      if (booking) {
        await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
        setFoundBooking(booking);
      } else {
        await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning);
        setFoundBooking(null);
        Alert.alert(
          'Không tìm thấy',
          `Không tìm thấy dữ liệu cho mã "${query}" trong bộ nhớ SQLite của máy.\n\nVui lòng kiểm tra lại mã hoặc tải trước manifest bến.`
        );
      }
    } catch (err: any) {
      setIsSearching(false);
      Alert.alert('Lỗi tra cứu', err.message);
    }
  };

  const handleOpenCheckIn = () => {
    if (!foundBooking) return;
    router.back();
    const verification = {
      isValid: true,
      publicBookingId: foundBooking.publicBookingId,
      algorithmUsed: 'HS256',
      verifiedOffline: true,
    };
    setCurrentBooking(foundBooking, verification);
  };

  return (
    <SafeAreaView style={styles.container}>
      {/* Top Header */}
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.closeBtn}>
          <Ionicons name="close" size={24} color="#0F172A" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Tra Cứu Vé & Booking Nhanh</Text>
        <View style={{ width: 32 }} />
      </View>

      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.desc}>
          Dùng khi màn hình điện thoại khách bị nứt, QR mờ camera không đọc được, hoặc khách chỉ nhớ mã đặt chỗ:
        </Text>

        {/* Input Box */}
        <View style={styles.inputContainer}>
          <Ionicons name="search" size={20} color="#94A3B8" style={{ marginRight: 8 }} />
          <TextInput
            style={styles.input}
            placeholder="VD: pub_wb_98a76b5c hoặc WB20261006001"
            value={code}
            onChangeText={setCode}
            autoCapitalize="none"
            autoCorrect={false}
          />
          {code.length > 0 && (
            <TouchableOpacity onPress={() => setCode('')}>
              <Ionicons name="close-circle" size={18} color="#94A3B8" />
            </TouchableOpacity>
          )}
        </View>

        {/* Search Action Button */}
        <TouchableOpacity
          style={[styles.searchBtn, isSearching && styles.btnDisabled]}
          onPress={() => handleSearch()}
          disabled={isSearching}
        >
          {isSearching ? (
            <ActivityIndicator color="#FFFFFF" size="small" />
          ) : (
            <>
              <Ionicons name="search" size={18} color="#FFFFFF" />
              <Text style={styles.searchBtnText}>Tra Cứu Trong SQLite</Text>
            </>
          )}
        </TouchableOpacity>

        {/* Quick Sample Buttons */}
        <Text style={styles.sampleTitle}>Hoặc chọn nhanh mã vé mẫu có sẵn:</Text>
        <View style={styles.sampleRow}>
          <TouchableOpacity
            style={styles.sampleChip}
            onPress={() => {
              setCode('pub_wb_98a76b5c');
              handleSearch('pub_wb_98a76b5c');
            }}
          >
            <Text style={styles.sampleChipText}>Trần Văn Minh (3 vé)</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.sampleChip}
            onPress={() => {
              setCode('pub_wb_11f22e33');
              handleSearch('pub_wb_11f22e33');
            }}
          >
            <Text style={styles.sampleChipText}>Nguyễn Thu Trang (2 vé)</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.sampleChip}
            onPress={() => {
              setCode('pub_wb_44d55c66');
              handleSearch('pub_wb_44d55c66');
            }}
          >
            <Text style={styles.sampleChipText}>Phạm Đức Huy (1 vé)</Text>
          </TouchableOpacity>
        </View>

        {/* Search Result Card */}
        {foundBooking && (
          <View style={styles.resultCard}>
            <View style={styles.resultHeader}>
              <View style={styles.badgeSuccess}>
                <Ionicons name="checkmark-circle" size={14} color="#059669" />
                <Text style={styles.badgeSuccessText}>ĐÃ TÌM THẤY TRONG MÁY</Text>
              </View>
              <Text style={styles.bookingCodeText}>{foundBooking.bookingCode}</Text>
            </View>

            <View style={styles.divider} />

            <View style={styles.row}>
              <Text style={styles.label}>Khách hàng:</Text>
              <Text style={styles.valueBold}>{foundBooking.customerName}</Text>
            </View>

            {foundBooking.customerPhone ? (
              <View style={styles.row}>
                <Text style={styles.label}>Số điện thoại:</Text>
                <Text style={styles.value}>{foundBooking.customerPhone}</Text>
              </View>
            ) : null}

            <View style={styles.row}>
              <Text style={styles.label}>Tổng số vé:</Text>
              <Text style={styles.valueBold}>{foundBooking.tickets.length} hành khách</Text>
            </View>

            <View style={styles.ticketList}>
              {foundBooking.tickets.map((t, idx) => {
                const isChecked = t.boardingStatus === 'CheckedIn';
                return (
                  <View key={t.id || idx} style={styles.ticketMiniItem}>
                    <Ionicons
                      name={isChecked ? 'checkmark-circle' : 'time-outline'}
                      size={16}
                      color={isChecked ? '#059669' : '#D97706'}
                    />
                    <Text style={styles.seatText}>Ghế {t.seatCode}</Text>
                    <Text style={styles.passengerText}>{t.passengerName}</Text>
                    <Text style={[styles.statusText, isChecked ? styles.textChecked : styles.textUnchecked]}>
                      {isChecked ? 'Đã lên tàu' : 'Chưa check-in'}
                    </Text>
                  </View>
                );
              })}
            </View>

            <TouchableOpacity style={styles.openCheckInBtn} onPress={handleOpenCheckIn}>
              <Ionicons name="checkbox-outline" size={18} color="#FFFFFF" />
              <Text style={styles.openCheckInBtnText}>Mở Màn Hình Tích Chọn Soát Vé</Text>
            </TouchableOpacity>
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: BrandColors.mist,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 14,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: BrandColors.border,
  },
  closeBtn: {
    padding: 4,
  },
  headerTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: BrandColors.ink,
  },
  content: {
    padding: 16,
    paddingBottom: 40,
  },
  desc: {
    fontSize: 13,
    color: BrandColors.textSecondary,
    lineHeight: 18,
    marginBottom: 16,
  },
  inputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: BrandColors.border,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginBottom: 12,
  },
  input: {
    flex: 1,
    fontSize: 15,
    color: BrandColors.ink,
  },
  searchBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: BrandColors.tealFlow,
    paddingVertical: 12,
    borderRadius: Radius.button,
    gap: 8,
    marginBottom: 20,
  },
  btnDisabled: {
    opacity: 0.6,
  },
  searchBtnText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
  sampleTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: BrandColors.textSecondary,
    marginBottom: 8,
  },
  sampleRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 20,
  },
  sampleChip: {
    backgroundColor: BrandColors.sandLight,
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: Radius.pill,
  },
  sampleChipText: {
    fontSize: 12,
    fontWeight: '600',
    color: BrandColors.ink,
  },
  resultCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: Radius.card,
    padding: 16,
    borderWidth: 1,
    borderColor: BrandColors.tealFlow,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.06,
    shadowRadius: 4,
    elevation: 2,
  },
  resultHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  badgeSuccess: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: BrandColors.successBg,
    paddingVertical: 4,
    paddingHorizontal: 8,
    borderRadius: Radius.pill,
    gap: 4,
  },
  badgeSuccessText: {
    fontSize: 11,
    fontWeight: '700',
    color: BrandColors.success,
  },
  bookingCodeText: {
    fontSize: 14,
    fontWeight: '700',
    color: BrandColors.tealFlow,
  },
  divider: {
    height: 1,
    backgroundColor: BrandColors.border,
    marginVertical: 12,
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 4,
  },
  label: {
    fontSize: 13,
    color: BrandColors.textSecondary,
  },
  value: {
    fontSize: 13,
    color: BrandColors.ink,
  },
  valueBold: {
    fontSize: 13,
    fontWeight: '700',
    color: BrandColors.ink,
  },
  ticketList: {
    marginTop: 10,
    backgroundColor: BrandColors.mist,
    borderRadius: Radius.md,
    padding: 10,
    gap: 8,
  },
  ticketMiniItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  seatText: {
    fontSize: 12,
    fontWeight: '700',
    color: BrandColors.tealFlow,
  },
  passengerText: {
    fontSize: 12,
    color: BrandColors.ink,
    flex: 1,
  },
  statusText: {
    fontSize: 11,
    fontWeight: '600',
  },
  textChecked: {
    color: BrandColors.success,
  },
  textUnchecked: {
    color: BrandColors.signalAmber,
  },
  openCheckInBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: BrandColors.deepRiver,
    paddingVertical: 12,
    borderRadius: Radius.button,
    marginTop: 16,
    gap: 6,
  },
  openCheckInBtnText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
});
