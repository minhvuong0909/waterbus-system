/**
 * Preload Manifest & Offline Cache Management Screen
 * Allows staff to preload trips, bookings, tickets, and public keys before gate opening (BR-OFF-01).
 * Displays cache metrics, batch sync status, loaded trip list, and QR test simulator.
 */

import React, { useState, useEffect, useCallback } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  SafeAreaView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { router } from 'expo-router';
import { useScannerStore } from '../../store/useScannerStore';
import { QrVerifier } from '../../services/crypto/qrVerifier';
import { SqliteRepository } from '../../services/sqlite/sqliteRepository';
import { Trip } from '../../types/scanner';

const STATIONS = [
  { id: 'ST-BACH-DANG', name: 'Bến Bạch Đằng (Quận 1)' },
  { id: 'ST-BINH-AN', name: 'Bến Bình An (TP. Thủ Đức)' },
  { id: 'ST-THU-THIEM', name: 'Bến Thủ Thiêm (TP. Thủ Đức)' },
  { id: 'ST-THANH-DA', name: 'Bến Thanh Đa (Bình Thạnh)' },
  { id: 'ST-LINH-DONG', name: 'Bến Linh Đông (TP. Thủ Đức)' },
];

type TripWithStats = Trip & { totalTickets: number; checkedInTickets: number };

export default function PreloadManifestScreen() {
  const {
    stats,
    deviceInfo,
    isPreloading,
    isSyncing,
    syncMessage,
    preloadCache,
    syncPendingBatch,
    refreshStats,
    clearCache,
  } = useScannerStore();

  const [selectedStationId, setSelectedStationId] = useState<string>(deviceInfo.stationId);
  const [testQrInfo, setTestQrInfo] = useState<{ compact: string; signature: string } | null>(null);
  const [trips, setTrips] = useState<TripWithStats[]>([]);

  const loadTrips = useCallback(async () => {
    try {
      const list = await SqliteRepository.getAllTrips();
      setTrips(list);
    } catch (err) {
      console.warn('[Manifest] Failed to load trips:', err);
    }
  }, []);

  useEffect(() => {
    refreshStats();
    loadTrips();
  }, [refreshStats, loadTrips]);

  const handlePreload = async () => {
    await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    const success = await preloadCache(selectedStationId);
    if (success) {
      await loadTrips();
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      Alert.alert(
        'Đã Tải Trước Thành Công',
        `Toàn bộ danh sách chuyến, vé hợp lệ và Public Key đã được lưu an toàn vào cơ sở dữ liệu SQLite.\nThiết bị đã sẵn sàng tác nghiệp soát vé ngoại tuyến 100%!`,
        [{ text: 'OK' }]
      );
    }
  };

  const handleSyncBatch = async () => {
    await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    const count = await syncPendingBatch();
    await loadTrips();
    if (count > 0) {
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      Alert.alert('Đồng Bộ Thành Công', `Đã đẩy lô ${count} sự kiện check-in lên máy chủ.`);
    } else {
      Alert.alert('Thông báo', 'Không có vé nào tồn đọng cần đồng bộ.');
    }
  };

  const handleGenerateTestQr = () => {
    const test = QrVerifier.generateTestSignedQr('pub_wb_98a76b5c', 'TRIP-BD-BA-01');
    setTestQrInfo({ compact: test.compactQr, signature: test.signature });
  };

  const handleClearCacheConfirm = () => {
    Alert.alert(
      'Xác nhận xoá cache',
      'Bạn có chắc chắn muốn xoá toàn bộ dữ liệu SQLite trên máy? Dữ liệu chưa sync có thể bị mất.',
      [
        { text: 'Huỷ', style: 'cancel' },
        {
          text: 'Xoá Cache',
          style: 'destructive',
          onPress: async () => {
            await clearCache();
            await loadTrips();
            Alert.alert('Đã xoá', 'Bộ nhớ đệm SQLite đã được làm sạch.');
          },
        },
      ]
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        {/* Screen Header */}
        <View style={styles.header}>
          <Text style={styles.title}>Quản Lý Bến & Tải Vé (Preload)</Text>
          <Text style={styles.subtitle}>
            Tải trước manifest chuyến, vé hợp lệ và Public Key vào bộ nhớ SQLite trước giờ mở cổng bến (BR-OFF-01)
          </Text>
        </View>

        {/* Station Selector */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Chọn Bến Mở Cổng Tác Nghiệp</Text>
          <View style={styles.stationList}>
            {STATIONS.map((st) => {
              const isSelected = selectedStationId === st.id;
              return (
                <TouchableOpacity
                  key={st.id}
                  style={[styles.stationChip, isSelected && styles.stationChipSelected]}
                  onPress={() => setSelectedStationId(st.id)}
                >
                  <Ionicons
                    name={isSelected ? 'radio-button-on' : 'radio-button-off'}
                    size={16}
                    color={isSelected ? '#0284C7' : '#64748B'}
                  />
                  <Text style={[styles.stationChipText, isSelected && styles.stationChipTextSelected]}>
                    {st.name}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>

          {/* Action Button: Preload Cache */}
          <TouchableOpacity
            style={[styles.preloadButton, isPreloading && styles.buttonDisabled]}
            onPress={handlePreload}
            disabled={isPreloading}
          >
            {isPreloading ? (
              <ActivityIndicator color="#FFFFFF" style={{ marginRight: 8 }} />
            ) : (
              <Ionicons name="cloud-download" size={20} color="#FFFFFF" style={{ marginRight: 8 }} />
            )}
            <Text style={styles.preloadButtonText}>
              {isPreloading ? 'ĐANG TẢI DỮ LIỆU...' : 'TẢI TRƯỚC DỮ LIỆU (PRELOAD CACHE)'}
            </Text>
          </TouchableOpacity>
        </View>

        {/* SQLite Database Statistics Card */}
        <View style={styles.card}>
          <View style={styles.cardHeaderRow}>
            <Text style={styles.cardTitle}>Bộ Nhớ Cục Bộ (SQLite)</Text>
            <TouchableOpacity onPress={() => { refreshStats(); loadTrips(); }} style={styles.refreshIconBtn}>
              <Ionicons name="refresh" size={18} color="#0284C7" />
            </TouchableOpacity>
          </View>

          <View style={styles.statsGrid}>
            <View style={styles.statBox}>
              <Text style={styles.statNumber}>{stats.tripCount}</Text>
              <Text style={styles.statLabel}>Chuyến Đi</Text>
            </View>
            <View style={styles.statBox}>
              <Text style={styles.statNumber}>{stats.bookingCount}</Text>
              <Text style={styles.statLabel}>Đơn Booking</Text>
            </View>
            <View style={styles.statBox}>
              <Text style={[styles.statNumber, { color: '#0284C7' }]}>{stats.ticketCount}</Text>
              <Text style={styles.statLabel}>Tổng Vé Cache</Text>
            </View>
            <View style={styles.statBox}>
              <Text style={[styles.statNumber, { color: '#059669' }]}>{stats.checkedInCount}</Text>
              <Text style={styles.statLabel}>Đã Check-in</Text>
            </View>
          </View>

          <View style={styles.statsDivider} />

          <View style={styles.infoRow}>
            <Text style={styles.infoLabel}>Vé chờ đồng bộ (Offline Queue):</Text>
            <Text style={[styles.infoValueBold, stats.pendingSyncCount > 0 && { color: '#EA580C' }]}>
              {stats.pendingSyncCount} sự kiện
            </Text>
          </View>
          <View style={styles.infoRow}>
            <Text style={styles.infoLabel}>Lần tải trước gần nhất:</Text>
            <Text style={styles.infoValue}>
              {stats.lastPreloadAt ? new Date(stats.lastPreloadAt).toLocaleString('vi-VN') : 'Chưa tải'}
            </Text>
          </View>
          <View style={styles.infoRow}>
            <Text style={styles.infoLabel}>Dung lượng SQLite:</Text>
            <Text style={styles.infoValue}>~{stats.databaseSizeKb} KB (Chế độ WAL)</Text>
          </View>

          {/* Sync Button */}
          <TouchableOpacity
            style={[styles.syncButton, (isSyncing || stats.pendingSyncCount === 0) && styles.buttonDisabled]}
            onPress={handleSyncBatch}
            disabled={isSyncing || stats.pendingSyncCount === 0}
          >
            {isSyncing ? (
              <ActivityIndicator color="#FFFFFF" style={{ marginRight: 8 }} />
            ) : (
              <Ionicons name="sync" size={18} color="#FFFFFF" style={{ marginRight: 8 }} />
            )}
            <Text style={styles.syncButtonText}>
              {isSyncing ? 'ĐANG ĐỒNG BỘ LÔ...' : `ĐẨY LÔ SYNC LÊN SERVER (${stats.pendingSyncCount})`}
            </Text>
          </TouchableOpacity>
        </View>

        {/* Sync message alert */}
        {syncMessage && (
          <View style={styles.messageBanner}>
            <Ionicons name="information-circle" size={18} color="#0284C7" />
            <Text style={styles.messageBannerText}>{syncMessage}</Text>
          </View>
        )}

        {/* Loaded Trips Section */}
        <View style={styles.card}>
          <View style={styles.cardHeaderRow}>
            <Text style={styles.cardTitle}>Danh Sách Chuyến Đã Nạp ({trips.length})</Text>
            <Text style={styles.cardSubtitle}>Ca trực hôm nay</Text>
          </View>

          {trips.length === 0 ? (
            <View style={styles.emptyTripsBox}>
              <Ionicons name="boat-outline" size={36} color="#CBD5E1" />
              <Text style={styles.emptyTripsText}>
                Chưa có chuyến nào được tải vào máy. Bấm nút "Tải Trước Dữ Liệu" ở trên để nạp chuyến và vé.
              </Text>
            </View>
          ) : (
            trips.map((trip) => {
              const progressPercent = trip.totalTickets > 0 ? (trip.checkedInTickets / trip.totalTickets) * 100 : 0;
              const depFormatted = new Date(trip.departureTime).toLocaleTimeString('vi-VN', {
                hour: '2-digit',
                minute: '2-digit',
              });

              return (
                <View key={trip.id} style={styles.tripItemCard}>
                  <View style={styles.tripItemTop}>
                    <View style={styles.tripCodeBadge}>
                      <Text style={styles.tripCodeBadgeText}>{trip.tripCode}</Text>
                    </View>
                    <View style={styles.tripTypeBadge}>
                      <Text style={styles.tripTypeBadgeText}>{trip.tripType}</Text>
                    </View>
                    <View style={{ flex: 1 }} />
                    <View style={styles.tripStatusBadge}>
                      <Ionicons name="radio-button-on" size={12} color="#059669" />
                      <Text style={styles.tripStatusText}>{trip.status}</Text>
                    </View>
                  </View>

                  <Text style={styles.tripRouteName}>{trip.routeName}</Text>

                  <View style={styles.tripDetailRow}>
                    <Ionicons name="boat" size={14} color="#64748B" />
                    <Text style={styles.tripDetailText}>{trip.boatName}</Text>
                    <Text style={styles.tripDetailDot}>•</Text>
                    <Ionicons name="time" size={14} color="#64748B" />
                    <Text style={styles.tripDetailText}>Xuất bến: {depFormatted}</Text>
                  </View>

                  {/* Boarding progress bar */}
                  <View style={styles.progressContainer}>
                    <View style={styles.progressRow}>
                      <Text style={styles.progressLabel}>Tiến độ soát vé:</Text>
                      <Text style={styles.progressValue}>
                        {trip.checkedInTickets} / {trip.totalTickets} khách
                      </Text>
                    </View>
                    <View style={styles.progressBarBg}>
                      <View style={[styles.progressBarFill, { width: `${progressPercent}%` }]} />
                    </View>
                  </View>

                  <TouchableOpacity
                    style={styles.scanTripButton}
                    onPress={() => router.push('/(tabs)')}
                  >
                    <Ionicons name="qr-code" size={16} color="#0284C7" />
                    <Text style={styles.scanTripButtonText}>Mở Camera Soát Chuyến Này</Text>
                  </TouchableOpacity>
                </View>
              );
            })
          )}
        </View>

        {/* Test QR Generator Card */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Tạo Mã QR Mẫu Kèm Chữ Ký Số (Demo)</Text>
          <Text style={styles.cardDesc}>
            Sinh chuỗi QR mẫu ký theo chuẩn HMAC-SHA256 để kiểm thử quét và giải mã ngoại tuyến:
          </Text>

          <TouchableOpacity style={styles.testGenButton} onPress={handleGenerateTestQr}>
            <Ionicons name="qr-code-outline" size={18} color="#0284C7" style={{ marginRight: 6 }} />
            <Text style={styles.testGenButtonText}>Sinh Chuỗi QR Hợp Lệ</Text>
          </TouchableOpacity>

          {testQrInfo && (
            <View style={styles.qrCodeBox}>
              <Text style={styles.qrCodeLabel}>Chuỗi QR đã ký (Dán vào camera hoặc nhập tay):</Text>
              <Text style={styles.qrCodeText} selectable>
                {testQrInfo.compact}
              </Text>
              <Text style={styles.signatureText} numberOfLines={1}>
                Chữ ký: {testQrInfo.signature}
              </Text>
            </View>
          )}
        </View>

        {/* Clear Cache Card */}
        <View style={styles.dangerCard}>
          <TouchableOpacity style={styles.clearCacheBtn} onPress={handleClearCacheConfirm}>
            <Ionicons name="trash-outline" size={18} color="#DC2626" style={{ marginRight: 6 }} />
            <Text style={styles.clearCacheBtnText}>Xoá Toàn Bộ Dữ Liệu SQLite Cục Bộ</Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F8FAFC',
  },
  content: {
    padding: 16,
    paddingBottom: 40,
  },
  header: {
    marginBottom: 16,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#0F172A',
  },
  subtitle: {
    fontSize: 13,
    color: '#64748B',
    marginTop: 4,
    lineHeight: 18,
  },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    padding: 16,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#0F172A',
  },
  cardSubtitle: {
    fontSize: 12,
    color: '#64748B',
  },
  cardDesc: {
    fontSize: 13,
    color: '#64748B',
    marginBottom: 12,
  },
  refreshIconBtn: {
    padding: 4,
  },
  stationList: {
    gap: 8,
    marginBottom: 16,
  },
  stationChip: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 10,
    paddingHorizontal: 14,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    backgroundColor: '#F8FAFC',
  },
  stationChipSelected: {
    borderColor: '#0284C7',
    backgroundColor: '#F0F9FF',
  },
  stationChipText: {
    fontSize: 14,
    color: '#334155',
    marginLeft: 10,
    fontWeight: '500',
  },
  stationChipTextSelected: {
    color: '#0284C7',
    fontWeight: '700',
  },
  preloadButton: {
    backgroundColor: '#0284C7',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 14,
    borderRadius: 10,
    marginTop: 6,
  },
  preloadButtonText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  statsGrid: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 14,
  },
  statBox: {
    flex: 1,
    backgroundColor: '#F8FAFC',
    paddingVertical: 10,
    paddingHorizontal: 6,
    borderRadius: 8,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#F1F5F9',
  },
  statNumber: {
    fontSize: 18,
    fontWeight: '800',
    color: '#0F172A',
  },
  statLabel: {
    fontSize: 11,
    color: '#64748B',
    marginTop: 2,
    textAlign: 'center',
  },
  statsDivider: {
    height: 1,
    backgroundColor: '#F1F5F9',
    marginBottom: 12,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 4,
  },
  infoLabel: {
    fontSize: 13,
    color: '#64748B',
  },
  infoValue: {
    fontSize: 13,
    color: '#0F172A',
  },
  infoValueBold: {
    fontSize: 13,
    fontWeight: '700',
    color: '#0F172A',
  },
  syncButton: {
    backgroundColor: '#0F172A',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 10,
    marginTop: 14,
  },
  syncButtonText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  messageBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0F9FF',
    borderWidth: 1,
    borderColor: '#BAE6FD',
    borderRadius: 10,
    padding: 12,
    marginBottom: 16,
    gap: 8,
  },
  messageBannerText: {
    fontSize: 13,
    color: '#0369A1',
    flex: 1,
  },
  emptyTripsBox: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 24,
    gap: 8,
  },
  emptyTripsText: {
    fontSize: 13,
    color: '#94A3B8',
    textAlign: 'center',
    lineHeight: 18,
    paddingHorizontal: 16,
  },
  tripItemCard: {
    backgroundColor: '#F8FAFC',
    borderRadius: 12,
    padding: 14,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: '#E2E8F0',
  },
  tripItemTop: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 6,
  },
  tripCodeBadge: {
    backgroundColor: '#0284C7',
    paddingVertical: 2,
    paddingHorizontal: 7,
    borderRadius: 4,
  },
  tripCodeBadgeText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '700',
  },
  tripTypeBadge: {
    backgroundColor: '#E2E8F0',
    paddingVertical: 2,
    paddingHorizontal: 7,
    borderRadius: 4,
  },
  tripTypeBadgeText: {
    color: '#475569',
    fontSize: 11,
    fontWeight: '600',
  },
  tripStatusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  tripStatusText: {
    fontSize: 12,
    color: '#059669',
    fontWeight: '600',
  },
  tripRouteName: {
    fontSize: 15,
    fontWeight: '700',
    color: '#0F172A',
    marginBottom: 4,
  },
  tripDetailRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    marginBottom: 10,
  },
  tripDetailText: {
    fontSize: 12,
    color: '#64748B',
  },
  tripDetailDot: {
    color: '#CBD5E1',
    marginHorizontal: 4,
  },
  progressContainer: {
    marginBottom: 10,
  },
  progressRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 4,
  },
  progressLabel: {
    fontSize: 11,
    color: '#64748B',
  },
  progressValue: {
    fontSize: 11,
    fontWeight: '700',
    color: '#0284C7',
  },
  progressBarBg: {
    height: 6,
    backgroundColor: '#E2E8F0',
    borderRadius: 3,
    overflow: 'hidden',
  },
  progressBarFill: {
    height: '100%',
    backgroundColor: '#0284C7',
    borderRadius: 3,
  },
  scanTripButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 8,
    borderRadius: 8,
    backgroundColor: '#E0F2FE',
    gap: 6,
  },
  scanTripButtonText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#0284C7',
  },
  testGenButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#0284C7',
    backgroundColor: '#F0F9FF',
  },
  testGenButtonText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#0284C7',
  },
  qrCodeBox: {
    marginTop: 12,
    backgroundColor: '#F8FAFC',
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: '#E2E8F0',
  },
  qrCodeLabel: {
    fontSize: 11,
    color: '#64748B',
    marginBottom: 4,
  },
  qrCodeText: {
    fontSize: 11,
    fontWeight: '600',
    color: '#0284C7',
  },
  signatureText: {
    fontSize: 10,
    color: '#94A3B8',
    marginTop: 4,
  },
  dangerCard: {
    marginTop: 4,
    marginBottom: 20,
  },
  clearCacheBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#FCA5A5',
    backgroundColor: '#FEF2F2',
  },
  clearCacheBtnText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#DC2626',
  },
});
