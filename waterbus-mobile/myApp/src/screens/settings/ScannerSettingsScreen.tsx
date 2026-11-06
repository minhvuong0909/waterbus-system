/**
 * Staff Scanner Settings & Diagnostics Screen
 * Device configuration, staff profile (Tuấn), force offline simulation toggle,
 * and backend server URL settings.
 */

import { useState } from 'react';
import {
  StyleSheet,
  Text,
  View,
  ScrollView,
  Switch,
  TextInput,
  TouchableOpacity,
  Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useScannerStore } from '../../store/useScannerStore';
import { getApiBaseUrl, setApiBaseUrl } from '../../api/client';
import { loginStaff, logoutStaff } from '../../services/auth/authService';
import { useAuthStore } from '../../store/useAuthStore';
import { BrandColors, Radius } from '../../../constants/theme';

export default function ScannerSettingsScreen() {
  const { deviceInfo, setDeviceInfo, setForceOfflineMode, syncPendingBatch } = useScannerStore();

  const { user, setUser, clearUser } = useAuthStore();
  const [backendUrl, setBackendUrl] = useState(getApiBaseUrl());
  const [deviceCode, setDeviceCode] = useState(deviceInfo.deviceCode);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  const handleSaveConfig = () => {
    setApiBaseUrl(backendUrl);
    setDeviceInfo({ deviceCode });
    Alert.alert('Đã lưu cấu hình', 'Thông tin thiết bị và máy chủ đã được cập nhật.');
  };

  const handleLogin = async () => {
    if (!email.trim() || !password) {
      Alert.alert('Thiếu thông tin', 'Vui lòng nhập email và mật khẩu tài khoản Staff.');
      return;
    }

    setIsLoggingIn(true);
    try {
      const staff = await loginStaff(email.trim(), password);
      setUser(staff);
      setDeviceInfo({ staffId: staff.userId, staffName: staff.fullName });
      setPassword('');
      Alert.alert('Đăng nhập thành công', `Xin chào ${staff.fullName}.`);
    } catch (error) {
      Alert.alert(
        'Không thể đăng nhập',
        error instanceof Error ? error.message : 'Vui lòng thử lại sau.'
      );
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleLogout = () => {
    logoutStaff();
    setDeviceInfo({ staffId: '', staffName: 'Chưa đăng nhập', isPrimaryOffline: false });
    clearUser();
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.title}>Cài đặt và thông tin nhân viên</Text>
          <Text style={styles.subtitle}>Thiết lập máy soát vé cầm tay</Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Tài khoản Staff</Text>
          {user ? (
            <>
              <Text style={styles.authenticatedText}>Đã đăng nhập: {user.fullName}</Text>
              <Text style={styles.metaValue}>{user.email}</Text>
              <TouchableOpacity style={styles.logoutBtn} onPress={handleLogout}>
                <Text style={styles.logoutBtnText}>Đăng xuất</Text>
              </TouchableOpacity>
            </>
          ) : (
            <>
              <Text style={styles.inputLabel}>Email</Text>
              <TextInput
                style={styles.input}
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                keyboardType="email-address"
                autoComplete="email"
              />
              <Text style={styles.inputLabel}>Mật khẩu</Text>
              <TextInput
                style={styles.input}
                value={password}
                onChangeText={setPassword}
                secureTextEntry
                autoComplete="password"
              />
              <TouchableOpacity
                style={[styles.saveBtn, isLoggingIn && styles.disabledBtn]}
                onPress={handleLogin}
                disabled={isLoggingIn}
              >
                <Text style={styles.saveBtnText}>
                  {isLoggingIn ? 'Đang đăng nhập...' : 'Đăng nhập'}
                </Text>
              </TouchableOpacity>
            </>
          )}
          <Text style={styles.authNote}>
            JWT chỉ được giữ trong bộ nhớ ứng dụng; đăng nhập lại sau khi khởi động lại app.
          </Text>
        </View>

        {/* Staff Profile Card */}
        <View style={styles.card}>
          <View style={styles.profileRow}>
            <View style={styles.avatar}>
              <Text style={styles.avatarText}>NV</Text>
            </View>
            <View style={styles.profileInfo}>
              <Text style={styles.staffName}>{deviceInfo.staffName}</Text>
              <Text style={styles.staffRole}>Nhân viên soát vé</Text>
              <Text style={styles.staffCode}>Mã nhân viên: {deviceInfo.staffId}</Text>
            </View>
          </View>

          <View style={styles.divider} />

          <View style={styles.metaRow}>
            <Text style={styles.metaLabel}>Bến phân công hiện tại:</Text>
            <Text style={styles.metaValue}>{deviceInfo.stationName}</Text>
          </View>
          <View style={styles.metaRow}>
            <Text style={styles.metaLabel}>Khả năng làm việc khi mất mạng:</Text>
            <View style={deviceInfo.isPrimaryOffline ? styles.badgePrimary : styles.badgeInactive}>
              <Text style={deviceInfo.isPrimaryOffline ? styles.badgePrimaryText : styles.badgeInactiveText}>
                {deviceInfo.isPrimaryOffline ? 'Đã nhận quyền chính' : 'Chưa nhận quyền chính'}
              </Text>
            </View>
          </View>
        </View>

        {/* Operating Mode (Offline vs Online) */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Kết nối mạng</Text>

          <View style={styles.switchRow}>
            <View style={{ flex: 1, paddingRight: 10 }}>
              <Text style={styles.switchTitle}>Chỉ lưu dữ liệu trên thiết bị</Text>
              <Text style={styles.switchDesc}>
                Khi bật, ứng dụng không gửi dữ liệu lên máy chủ. Dữ liệu sẽ được lưu trên thiết bị để sử dụng khi mất kết nối.
              </Text>
            </View>
            <Switch
              value={deviceInfo.forceOfflineMode}
              onValueChange={setForceOfflineMode}
              trackColor={{ false: '#CBD5E1', true: BrandColors.coralGlow }}
              thumbColor={deviceInfo.forceOfflineMode ? '#FFFFFF' : '#F8FAFC'}
            />
          </View>

          <View style={styles.divider} />

          <View style={styles.switchRow}>
            <View style={{ flex: 1, paddingRight: 10 }}>
              <Text style={styles.switchTitle}>Tự động gửi dữ liệu</Text>
              <Text style={styles.switchDesc}>
                Ứng dụng tự gửi các lượt soát vé đang chờ mỗi 15 giây khi có kết nối mạng.
              </Text>
            </View>
            <View style={styles.statusActiveBadge}>
              <Text style={styles.statusActiveText}>ĐANG CHẠY</Text>
            </View>
          </View>
        </View>

        {/* Device & Backend Config */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Thông tin thiết bị</Text>

          <Text style={styles.inputLabel}>Mã thiết bị:</Text>
          <TextInput
            style={styles.input}
            value={deviceCode}
            onChangeText={setDeviceCode}
            autoCapitalize="characters"
          />

          <Text style={styles.inputLabel}>Địa chỉ máy chủ:</Text>
          <TextInput
            style={styles.input}
            value={backendUrl}
            onChangeText={setBackendUrl}
            autoCapitalize="none"
            keyboardType="url"
            placeholder="https://waterbus.example"
          />

          <TouchableOpacity style={styles.saveBtn} onPress={handleSaveConfig}>
            <Ionicons name="save-outline" size={18} color="#FFFFFF" style={{ marginRight: 6 }} />
            <Text style={styles.saveBtnText}>Lưu Thay Đổi</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Tính năng hỗ trợ</Text>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>Mã vé được xác thực trước khi ghi nhận khách lên tàu.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>Có thể soát vé khi không có kết nối mạng.</Text>
          </View>
          <View style={styles.specItem}>
            <Ionicons name="checkmark-circle" size={16} color="#059669" />
            <Text style={styles.specText}>Dữ liệu sẽ được gửi lên máy chủ khi có kết nối mạng.</Text>
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: BrandColors.mist,
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
    fontWeight: '800',
    color: BrandColors.ink,
  },
  subtitle: {
    fontSize: 13,
    color: BrandColors.textSecondary,
    marginTop: 4,
  },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: Radius.card,
    padding: 16,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: BrandColors.border,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 2,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: BrandColors.ink,
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
    backgroundColor: BrandColors.deepRiver,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 14,
  },
  avatarText: {
    color: BrandColors.skyAqua,
    fontWeight: '800',
    fontSize: 18,
  },
  profileInfo: {
    flex: 1,
  },
  staffName: {
    fontSize: 16,
    fontWeight: '700',
    color: BrandColors.ink,
  },
  staffRole: {
    fontSize: 13,
    color: BrandColors.tealFlow,
    fontWeight: '600',
    marginTop: 2,
  },
  staffCode: {
    fontSize: 12,
    color: BrandColors.textSecondary,
    marginTop: 2,
  },
  divider: {
    height: 1,
    backgroundColor: BrandColors.border,
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
    color: BrandColors.textSecondary,
  },
  metaValue: {
    fontSize: 13,
    color: BrandColors.ink,
    fontWeight: '600',
  },
  badgePrimary: {
    backgroundColor: BrandColors.successBg,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: Radius.sm,
  },
  badgePrimaryText: {
    color: BrandColors.success,
    fontSize: 11,
    fontWeight: '700',
  },
  badgeInactive: {
    backgroundColor: BrandColors.mist,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: Radius.sm,
  },
  badgeInactiveText: {
    color: BrandColors.textSecondary,
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
    color: BrandColors.ink,
  },
  switchDesc: {
    fontSize: 12,
    color: BrandColors.textSecondary,
    marginTop: 4,
    lineHeight: 16,
  },
  statusActiveBadge: {
    backgroundColor: '#F0FDF9',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: Radius.sm,
    borderWidth: 1,
    borderColor: BrandColors.tealFlow,
  },
  statusActiveText: {
    color: BrandColors.tealFlow,
    fontSize: 11,
    fontWeight: '700',
  },
  inputLabel: {
    fontSize: 13,
    color: BrandColors.ink,
    marginBottom: 6,
    fontWeight: '500',
  },
  input: {
    borderWidth: 1.5,
    borderColor: BrandColors.border,
    borderRadius: Radius.sm,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    marginBottom: 12,
    backgroundColor: BrandColors.mist,
    color: BrandColors.ink,
  },
  saveBtn: {
    backgroundColor: BrandColors.tealFlow,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 12,
    borderRadius: Radius.button,
    marginTop: 4,
  },
  saveBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  authenticatedText: {
    color: BrandColors.ink,
    fontSize: 14,
    fontWeight: '700',
  },
  authNote: {
    color: BrandColors.textSecondary,
    fontSize: 12,
    lineHeight: 17,
    marginTop: 10,
  },
  logoutBtn: {
    alignItems: 'center',
    borderWidth: 1,
    borderColor: BrandColors.border,
    borderRadius: Radius.button,
    marginTop: 12,
    paddingVertical: 11,
  },
  logoutBtnText: {
    color: BrandColors.ink,
    fontSize: 14,
    fontWeight: '700',
  },
  disabledBtn: {
    opacity: 0.6,
  },
  specItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginVertical: 4,
  },
  specText: {
    fontSize: 13,
    color: BrandColors.ink,
  },
});
