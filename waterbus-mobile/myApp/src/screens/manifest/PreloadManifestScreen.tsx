/**
 * Preload Manifest & Offline Cache Management Screen
 * Allows staff to preload trips, bookings, tickets, and public keys before gate opening (BR-OFF-01).
 * Displays cache metrics, batch sync status, and QR test simulator.
 */

import React, { useState, useEffect } from 'react';
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
import { useScannerStore } from '../../store/useScannerStore';
import { QrVerifier } from '../../services/crypto/qrVerifier';

const STATIONS = [
  { id: 'ST-BACH-DANG', name: 'Bến Bạch Đằng (Quận 1)' },
  { id: 'ST-BINH-AN', name: 'Bến Bình An (TP. Thủ Đức)' },
  { id: 'ST-THU-THIEM', name: 'Bến Thủ Thiêm (TP. Thủ Đức)' },
  { id: 'ST-THANH-DA', name: 'Bến Thanh Đa (Bình Thạnh)' },
  { id: 'ST-LINH-DONG', name: 'Bến Linh Đông (TP. Thủ Đức)' },
];

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

  useEffect(() => {
    refreshStats();
  }, []);

  const handlePreload = async () => {
    await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    const success = await preloadCache(selectedStationId);
    if (success) {
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
          <Text style={styles.title}>Quản Lý Bộ Nhớ Đệm & Preload</Text>
          <Text style={styles.subtitle}>
            Tải dữ liệu vé và Public Key vào máy trước giờ mở cổng bến (BR-OFF-01)
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
            <Text style={styles.cardTitle}>Trạng Thái Bộ Nhớ Cục Bộ (SQLite)</Text>
            <TouchableOpacity onPress={refreshStats} style={styles.refreshIconBtn}>
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
              {stats.lastPreloadAt ? new Date(stats.lastPreloadAt).toLocaleString() : 'Chưa tải'}
            </Text>
          </View>
          <View style={styles.infoRow}>
            <Text style={styles.infoLabel}>Dung lượng SQLite:</Text>
            <Text style={styles.infoValue}>~{stats.databaseSizeKb} KB</Text>
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

        {/* Test QR Generator Card */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Tạo Mã QR Mẫu Kèm Chữ Ký Số</Text>
          <Text style={styles.cardDesc}>
            Sinh chuỗi QR mẫu ký theo chuẩn RFC 6238 / Waterbus HMAC-SHA256 để kiểm thử quét:
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
    marginBottom: 10,
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
    padding: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E2E8F0',
    backgroundColor: '#F8FAFC',
    gap: 8,
  },
  stationChipSelected: {
    borderColor: '#0284C7',
    backgroundColor: '#F0F9FF',
  },
  stationChipText: {
    fontSize: 14,
    color: '#334155',
  },
  stationChipTextSelected: {
    color: '#0284C7',
    fontWeight: '600',
  },
  preloadButton: {
    backgroundColor: '#0284C7',
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 13,
    borderRadius: 10,
  },
  preloadButtonText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
  statsGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginVertical: 6,
  },
  statBox: {
    alignItems: 'center',
    flex: 1,
  },
  statNumber: {
    fontSize: 22,
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
    marginVertical: 12,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginVertical: 4,
  },
  infoLabel: {
    fontSize: 13,
    color: '#64748B',
  },
  infoValue: {
    fontSize: 13,
    color: '#1E293B',
    fontWeight: '500',
  },
  infoValueBold: {
    fontSize: 13,
    color: '#0F172A',
    fontWeight: '700',
  },
  syncButton: {
    backgroundColor: '#059669',
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 11,
    borderRadius: 8,
    marginTop: 14,
  },
  syncButtonText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  buttonDisabled: {
    backgroundColor: '#94A3B8',
  },
  messageBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#EFF6FF',
    borderWidth: 1,
    borderColor: '#BFDBFE',
    borderRadius: 8,
    padding: 10,
    marginBottom: 16,
    gap: 8,
  },
  messageBannerText: {
    color: '#1D4ED8',
    fontSize: 12,
    flex: 1,
  },
  testGenButton: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 9,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#0284C7',
    backgroundColor: '#F0F9FF',
  },
  testGenButtonText: {
    color: '#0284C7',
    fontWeight: '600',
    fontSize: 13,
  },
  qrCodeBox: {
    marginTop: 12,
    backgroundColor: '#F1F5F9',
    padding: 10,
    borderRadius: 8,
  },
  qrCodeLabel: {
    fontSize: 11,
    color: '#64748B',
    marginBottom: 4,
  },
  qrCodeText: {
    fontSize: 12,
    fontFamily: 'monospace',
    color: '#0F172A',
    backgroundColor: '#FFFFFF',
    padding: 8,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#CBD5E1',
  },
  signatureText: {
    fontSize: 11,
    color: '#94A3B8',
    marginTop: 4,
  },
  dangerCard: {
    marginTop: 8,
    alignItems: 'center',
  },
  clearCacheBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 10,
  },
  clearCacheBtnText: {
    color: '#DC2626',
    fontSize: 13,
    fontWeight: '600',
  },
});

