/**
 * Staff Scanner Settings & Diagnostics Screen
 * Device configuration, staff profile (Tuấn), force offline simulation toggle,
 * and backend server URL settings.
 */

import React, { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  Switch,
  TextInput,
  TouchableOpacity,
  SafeAreaView,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useScannerStore } from '../../store/useScannerStore';
import { syncService } from '../../services/sync/syncService';

export default function ScannerSettingsScreen() {
  const { deviceInfo, setForceOfflineMode, syncPendingBatch } = useScannerStore();

  const [backendUrl, setBackendUrl] = useState('http://localhost:5000');
  const [deviceCode, setDeviceCode] = useState(deviceInfo.deviceCode);

  const handleSaveConfig = () => {
    syncService.setDeviceInfo({ deviceCode });
    Alert.alert('Đã lưu cấu hình', 'Thông tin thiết bị và máy chủ đã được cập nhật.');
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.title}>Cấu Hình & Thông Tin Nhân Viên</Text>
          <Text style={styles.subtitle}>Thiết lập thiết bị soát vé cầm tay (Staff Scanner)</Text>
        </View>

        {/* Staff Profile Card */}
        <View style={styles.card}>
          <View style={styles.profileRow}>
            <View style={styles.avatar}>
              <Text style={styles.avatarText}>NV</Text>
            </View>
            <View style={styles.profileInfo}>
              <Text style={styles.staffName}>{deviceInfo.staffName}</Text>
              <Text style={styles.staffRole}>Nhân Viên Soát Vé (Thành viên 4)</Text>
              <Text style={styles.staffCode}>Mã NV: {deviceInfo.staffId}</Text>
            </View>
          </View>

          <View style={styles.divider} />

          <View style={styles.metaRow}>
            <Text style={styles.metaLabel}>Bến phân công hiện tại:</Text>
            <Text style={styles.metaValue}>{deviceInfo.stationName}</Text>
          </View>
          <View style={styles.metaRow}>
            <Text style={styles.metaLabel}>Quyền ghi ngoại tuyến:</Text>
            <View style={styles.badgePrimary}>
              <Text style={styles.badgePrimaryText}>Primary Offline Scanner</Text>
            </View>
          </View>
        </View>

        {/* Operating Mode (Offline vs Online) */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Chế Độ Vận Hành Mạng</Text>

          <View style={styles.switchRow}>
            <View style={{ flex: 1, paddingRight: 10 }}>
              <Text style={styles.switchTitle}>Mô Phỏng Ngoại Tuyến (Force Offline)</Text>
              <Text style={styles.switchDesc}>
                Khi bật, ứng dụng sẽ không gửi dữ liệu ra ngoài và chỉ lưu 100% vào SQLite cục bộ để kiểm thử bến mất mạng.
              </Text>
            </View>
            <Switch
              value={deviceInfo.forceOfflineMode}
              onValueChange={setForceOfflineMode}
              trackColor={{ false: '#CBD5E1', true: '#EF4444' }}
              thumbColor={deviceInfo.forceOfflineMode ? '#FFFFFF' : '#F8FAFC'}
            />
          </View>

          <View style={styles.divider} />

          <View style={styles.switchRow}>
            <View style={{ flex: 1, paddingRight: 10 }}>
              <Text style={styles.switchTitle}>Tự Động Đồng Bộ (Idempotent Sync)</Text>
              <Text style={styles.switchDesc}>
                Background Task tự động quét hàng đợi và đẩy lô mỗi 15 giây khi có kết nối 3G/WiFi.
              </Text>
            </View>
            <View style={styles.statusActiveBadge}>
              <Text style={styles.statusActiveText}>ĐANG CHẠY</Text>
            </View>
          </View>
        </View>

        {/* Device & Backend Config */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Thông Số Kỹ Thuật Thiết Bị</Text>

          <Text style={styles.inputLabel}>Mã định danh máy quét (Device Code):</Text>
          <TextInput
            style={styles.input}
            value={deviceCode}
            onChangeText={setDeviceCode}
            autoCapitalize="characters"
          />

          <Text style={styles.inputLabel}>Địa chỉ Máy Chủ Backend (API Gateway):</Text>
          <TextInput
            style={styles.input}
            value={backendUrl}
            onChangeText={setBackendUrl}
            autoCapitalize="none"
            placeholder="http://localhost:5000"
          />

          <TouchableOpacity style={styles.saveBtn} onPress={handleSaveConfig}>
            <Ionicons name="save-outline" size={18} color="#FFFFFF" style={{ marginRight: 6 }} />
            <Text style={styles.saveBtnText}>Lưu Thay Đổi</Text>
          </TouchableOpacity>
        </View>

        {/* Security & Specification Info */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Quy Chuẩn Kỹ Thuật (BR v2.0)</Text>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>BR-QR-01: QR tĩnh ký số per-Booking.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>BR-QR-02: Xác thực chữ ký số bằng Public Key offline.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>BR-QR-03: Nhân viên tích chọn từng khách có mặt.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>BR-OFF-01: SQLite Preload cache trước giờ mở cổng.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>BR-OFF-03: ClientEventId Unique chống trùng lặp (Idempotent).</Text>
          </View>
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
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#0F172A',
    marginBottom: 12,
  },
  profileRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  avatar: {
    width: 52,
    height: 52,
    borderRadius: 26,
    backgroundColor: '#0284C7',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 14,
  },
  avatarText: {
    color: '#FFFFFF',
    fontWeight: '800',
    fontSize: 18,
  },
  profileInfo: {
    flex: 1,
  },
  staffName: {
    fontSize: 16,
    fontWeight: '700',
    color: '#0F172A',
  },
  staffRole: {
    fontSize: 13,
    color: '#0284C7',
    fontWeight: '600',
    marginTop: 2,
  },
  staffCode: {
    fontSize: 12,
    color: '#64748B',
    marginTop: 2,
  },
  divider: {
    height: 1,
    backgroundColor: '#F1F5F9',
    marginVertical: 12,
  },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginVertical: 4,
  },
  metaLabel: {
    fontSize: 13,
    color: '#64748B',
  },
  metaValue: {
    fontSize: 13,
    color: '#0F172A',
    fontWeight: '600',
  },
  badgePrimary: {
    backgroundColor: '#DCFCE7',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 6,
  },
  badgePrimaryText: {
    color: '#15803D',
    fontSize: 11,
    fontWeight: '700',
  },
  switchRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginVertical: 4,
  },
  switchTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#0F172A',
  },
  switchDesc: {
    fontSize: 12,
    color: '#64748B',
    marginTop: 4,
    lineHeight: 16,
  },
  statusActiveBadge: {
    backgroundColor: '#E0F2FE',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  statusActiveText: {
    color: '#0284C7',
    fontSize: 11,
    fontWeight: '700',
  },
  inputLabel: {
    fontSize: 13,
    color: '#475569',
    marginBottom: 6,
    fontWeight: '500',
  },
  input: {
    borderWidth: 1.5,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    marginBottom: 12,
    backgroundColor: '#F8FAFC',
  },
  saveBtn: {
    backgroundColor: '#0F172A',
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 4,
  },
  saveBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  specItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginVertical: 4,
  },
  specText: {
    fontSize: 13,
    color: '#334155',
  },
});

