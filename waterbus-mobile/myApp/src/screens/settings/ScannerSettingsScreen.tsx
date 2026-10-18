/**
 * Staff Scanner Settings & Diagnostics Screen
 * Device configuration, staff profile (Tuấn), force offline simulation toggle,
 * and backend server URL settings.
 */

import React, { useCallback, useEffect, useState } from 'react';
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
  ActivityIndicator,
} from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useScannerStore } from '../../store/useScannerStore';
import { useAuthStore } from '../../store/useAuthStore';
import { syncService } from '../../services/sync/syncService';
import { getMyScannerAssignments } from '../../services/checkin/scannerAssignmentService';
import { MyScannerAssignment } from '../../types/checkin';
import { ApiError } from '../../services/api/apiClient';

export default function ScannerSettingsScreen() {
  const { deviceInfo, setForceOfflineMode } = useScannerStore();
  const { user, logout } = useAuthStore();

  const [backendUrl, setBackendUrl] = useState('http://localhost:5000');
  const [deviceCode, setDeviceCode] = useState(deviceInfo.deviceCode);

  const [assignments, setAssignments] = useState<MyScannerAssignment[]>([]);
  const [isLoadingAssignments, setIsLoadingAssignments] = useState(false);
  const [assignmentsError, setAssignmentsError] = useState<string | null>(null);

  const loadAssignments = useCallback(async () => {
    setIsLoadingAssignments(true);
    setAssignmentsError(null);
    try {
      const result = await getMyScannerAssignments();
      setAssignments(result);
    } catch (err) {
      setAssignmentsError(err instanceof ApiError ? err.message : 'Không tải được phân công. Kiểm tra kết nối mạng.');
    } finally {
      setIsLoadingAssignments(false);
    }
  }, []);

  useEffect(() => {
    loadAssignments();
  }, [loadAssignments]);

  const handleSaveConfig = () => {
    syncService.setDeviceInfo({ deviceCode });
    Alert.alert('Đã lưu cấu hình', 'Thông tin thiết bị và máy chủ đã được cập nhật.');
  };

  const handleLogout = () => {
    Alert.alert('Đăng xuất', 'Bạn có chắc muốn đăng xuất khỏi tài khoản này?', [
      { text: 'Huỷ', style: 'cancel' },
      {
        text: 'Đăng xuất',
        style: 'destructive',
        onPress: async () => {
          await logout();
          router.replace('/login');
        },
      },
    ]);
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.title}>Cấu Hình & Thông Tin Nhân Viên</Text>
          <Text style={styles.subtitle}>Thiết lập thiết bị soát vé cầm tay (Staff Scanner)</Text>
        </View>

        {/* Staff Profile Card - tài khoản đăng nhập thật qua POST /api/v1/auth/login */}
        <View style={styles.card}>
          <View style={styles.profileRow}>
            <View style={styles.avatar}>
              <Text style={styles.avatarText}>
                {(user?.fullName || '?').trim().charAt(0).toUpperCase()}
              </Text>
            </View>
            <View style={styles.profileInfo}>
              <Text style={styles.staffName}>{user?.fullName || 'Chưa đăng nhập'}</Text>
              <Text style={styles.staffRole}>Nhân Viên Soát Vé (Staff)</Text>
              <Text style={styles.staffCode}>{user?.email}</Text>
            </View>
          </View>

          <View style={styles.divider} />

          <TouchableOpacity style={styles.logoutBtn} onPress={handleLogout}>
            <Ionicons name="log-out-outline" size={18} color="#DC2626" style={{ marginRight: 6 }} />
            <Text style={styles.logoutBtnText}>Đăng Xuất</Text>
          </TouchableOpacity>
        </View>

        {/* Phân công soát vé của tôi - GET /api/v1/checkins/assignments/mine (dữ liệu thật) */}
        <View style={styles.card}>
          <View style={styles.cardHeaderRow}>
            <Text style={styles.cardTitle}>Phân Công Soát Vé Của Tôi</Text>
            <TouchableOpacity onPress={loadAssignments} disabled={isLoadingAssignments}>
              <Ionicons name="refresh" size={18} color="#0284C7" />
            </TouchableOpacity>
          </View>

          {isLoadingAssignments ? (
            <ActivityIndicator color="#0284C7" style={{ marginVertical: 12 }} />
          ) : assignmentsError ? (
            <Text style={styles.assignmentError}>{assignmentsError}</Text>
          ) : assignments.length === 0 ? (
            <Text style={styles.assignmentEmpty}>Hiện chưa được phân công điểm dừng nào.</Text>
          ) : (
            assignments.map((a) => (
              <View key={a.assignmentId} style={styles.assignmentItem}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.assignmentStation}>{a.stationName}</Text>
                  <Text style={styles.assignmentRoute}>
                    {a.routeName} • Bến thứ {a.visitOrder} • {a.tripStatus}
                  </Text>
                  {a.checkInOpenAt && a.checkInCloseAt ? (
                    <Text style={styles.assignmentWindow}>
                      Check-in: {new Date(a.checkInOpenAt).toLocaleTimeString()} - {new Date(a.checkInCloseAt).toLocaleTimeString()}
                    </Text>
                  ) : null}
                </View>
                {a.isPrimaryOffline ? (
                  <View style={styles.badgePrimary}>
                    <Text style={styles.badgePrimaryText}>Primary Offline</Text>
                  </View>
                ) : null}
              </View>
            ))
          )}
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
  logoutBtn: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 10,
    borderRadius: 8,
    backgroundColor: '#FEF2F2',
  },
  logoutBtnText: {
    color: '#DC2626',
    fontWeight: '700',
    fontSize: 14,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  assignmentError: {
    color: '#DC2626',
    fontSize: 13,
    marginVertical: 8,
  },
  assignmentEmpty: {
    color: '#94A3B8',
    fontSize: 13,
    marginVertical: 8,
  },
  assignmentItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 10,
    borderTopWidth: 1,
    borderTopColor: '#F1F5F9',
  },
  assignmentStation: {
    fontSize: 14,
    fontWeight: '700',
    color: '#0F172A',
  },
  assignmentRoute: {
    fontSize: 12,
    color: '#64748B',
    marginTop: 2,
  },
  assignmentWindow: {
    fontSize: 11,
    color: '#0284C7',
    marginTop: 2,
  },
});

