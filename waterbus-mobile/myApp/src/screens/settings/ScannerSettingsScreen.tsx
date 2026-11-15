import React, { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import { router } from 'expo-router';
import { useIsFocused } from 'expo-router/react-navigation';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuthStore } from '../../store/useAuthStore';
import { useScannerStore } from '../../store/useScannerStore';
import { pairScanner } from '../../services/checkin/scannerPairingService';

const pairingText = {
  Unpaired: 'Chưa ghép nối',
  Paired: 'Đã ghép nối',
  Unverified: 'Đã lưu ghép nối · chưa xác minh với máy chủ',
  Invalid: 'Ghép nối đã hết hiệu lực hoặc tài khoản không còn quyền',
};

const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });

export default function ScannerSettingsScreen() {
  const { user, logout } = useAuthStore();
  const { deviceInfo, pairingStatus, shift, loadPairing, clearCache, isCheckingIn, isPreloading,
    stats, needsReviewCount, syncPendingBatch, isSyncing, syncMessage } = useScannerStore();
  const focused = useIsFocused();
  const [permission, requestPermission] = useCameraPermissions();
  const [showCamera, setShowCamera] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const isProcessing = useRef(false);

  useEffect(() => {
    if (focused) void loadPairing();
  }, [focused, loadPairing]);

  async function openCamera() {
    if (Platform.OS === 'web') {
      setError('Hãy dùng ứng dụng trên Android hoặc iOS để ghép nối.');
      return;
    }
    setError('');
    const allowed = permission?.granted || (await requestPermission()).granted;
    if (!allowed) {
      setError('Hãy cấp quyền camera trong cài đặt điện thoại.');
      return;
    }
    setShowCamera(true);
  }

  async function redeem(activation: string) {
    if (isProcessing.current) return;
    isProcessing.current = true;
    setBusy(true);
    setShowCamera(false);
    setError('');
    try {
      await pairScanner(activation);
      await clearCache();
      await loadPairing();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Ghép nối thất bại.');
    } finally {
      setBusy(false);
      isProcessing.current = false;
    }
  }

  function confirmLogout() {
    Alert.alert('Đăng xuất', 'Các sự kiện chờ đồng bộ vẫn được giữ cho tài khoản đã ghi nhận.', [
      { text: 'Huỷ', style: 'cancel' },
      { text: 'Đăng xuất', style: 'destructive', onPress: async () => {
        try { await logout(); } finally { router.replace('/login'); }
      } },
    ]);
  }

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>Cài đặt máy quét</Text>
        <View style={styles.card}>
          <Text style={styles.heading}>Thiết bị</Text>
          <Text accessibilityLiveRegion="polite" style={styles.body}>{pairingText[pairingStatus]}</Text>
          {deviceInfo.deviceCode ? <Text style={styles.body}>{deviceInfo.deviceCode} · {deviceInfo.stationName}</Text> : null}
          <Text style={styles.body}>Quản trị viên cấp QR kích hoạt cho máy tại bến.</Text>
          <Action title={deviceInfo.deviceCode ? 'Quét QR để ghép nối lại' : 'Quét QR kích hoạt'}
            onPress={openCamera} disabled={busy || isCheckingIn || isPreloading} />
          <Action title="Kiểm tra trạng thái" onPress={loadPairing} disabled={busy} />
          {busy && <ActivityIndicator color="#0284C7" />}
          {error ? <Text accessibilityRole="alert" style={styles.error}>{error}</Text> : null}
        </View>

        <View style={styles.card}>
          <Text style={styles.heading}>{user?.fullName ?? 'Nhân viên soát vé'}</Text>
          <Text style={styles.body}>{user?.email}</Text>
          <Text style={styles.body}>{shift
            ? `Ca tại ${deviceInfo.stationName} · ${formatTime(shift.activeFrom)} – ${formatTime(shift.activeUntil)}`
            : 'Chưa có ca làm việc đang hiệu lực tại bến của máy.'}</Text>
          <Action title="Đăng xuất" onPress={confirmLogout} disabled={busy || isCheckingIn || isPreloading} />
        </View>

        <View style={styles.card}>
          <Text style={styles.heading}>Đồng bộ</Text>
          <Text style={styles.body}>{stats.pendingSyncCount} sự kiện chờ · {needsReviewCount} cần đối soát</Text>
          <Action title={isSyncing ? 'Đang đồng bộ' : 'Đồng bộ ngay'} onPress={syncPendingBatch} disabled={isSyncing || (!stats.pendingSyncCount && !needsReviewCount)} />
          {syncMessage && <Text style={styles.body}>{syncMessage}</Text>}
        </View>
      </ScrollView>
      <Modal visible={showCamera && focused} animationType="slide" onRequestClose={() => setShowCamera(false)}>
        {showCamera && focused && <SafeAreaView style={styles.cameraScreen}>
          <Text style={styles.cameraText}>Hướng camera vào mã QR kích hoạt của quản trị viên.</Text>
          <CameraView style={styles.camera} barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
            onBarcodeScanned={({ data }) => void redeem(data)} />
          <Action title="Huỷ quét" onPress={() => setShowCamera(false)} />
        </SafeAreaView>}
      </Modal>
    </SafeAreaView>
  );
}

function Action({ title, onPress, disabled }: { title: string; onPress: () => unknown; disabled?: boolean }) {
  return (
    <Pressable accessibilityRole="button" accessibilityState={{ disabled }} disabled={disabled}
      onPress={onPress} style={[styles.action, disabled && styles.disabled]}>
      <Text style={styles.actionText}>{title}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F8FAFC' },
  content: { padding: 16, gap: 14 },
  title: { fontSize: 26, fontWeight: '700', color: '#0F172A' },
  card: { padding: 16, borderRadius: 12, backgroundColor: '#FFFFFF', gap: 8 },
  heading: { fontSize: 18, fontWeight: '700', color: '#0F172A' },
  body: { color: '#334155', lineHeight: 20 },
  error: { color: '#991B1B' },
  action: { backgroundColor: '#0369A1', minHeight: 44, justifyContent: 'center', alignItems: 'center', borderRadius: 8, padding: 10 },
  actionText: { color: '#FFFFFF', fontWeight: '600' },
  disabled: { opacity: 0.5 },
  cameraScreen: { flex: 1, backgroundColor: '#0F172A', padding: 16, gap: 12 },
  camera: { flex: 1 },
  cameraText: { color: '#FFFFFF', fontSize: 16 },
});
