/**
 * Staff Scanner Screen (Tuấn - Thành viên 4)
 * High-speed QR Camera Scanner with reticle animation, flash toggle,
 * instant offline digital signature verification & SQLite lookup.
 */

import React, { useState, useEffect, useRef } from 'react';
import {
  StyleSheet,
  Text,
  View,
  TouchableOpacity,
  Dimensions,
  Animated,
  Platform,
  Alert,
  TextInput,
  Modal,
} from 'react-native';
import { CameraView, useCameraPermissions, BarcodeScanningResult } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { Ionicons } from '@expo/vector-icons';
import { useScannerStore } from '../../store/useScannerStore';
import { QrVerifier } from '../../services/crypto/qrVerifier';
import { SqliteRepository } from '../../services/sqlite/sqliteRepository';
import BookingDetailModal from '../booking/BookingDetailModal';
import { BrandColors, Radius } from '../../../constants/theme';

const { width, height } = Dimensions.get('window');
const SCAN_AREA_SIZE = width * 0.72;

export default function StaffScannerScreen() {
  const [permission, requestPermission] = useCameraPermissions();
  const [facing, setFacing] = useState<'back' | 'front'>('back');
  const [torch, setTorch] = useState<boolean>(false);
  const [isScannedLocked, setIsScannedLocked] = useState<boolean>(false);
  const [manualModalOpen, setManualModalOpen] = useState<boolean>(false);
  const [manualCode, setManualCode] = useState<string>('');
  const [testModalOpen, setTestModalOpen] = useState<boolean>(false);

  const {
    setCurrentBooking,
    stats,
    deviceInfo,
    refreshStats,
    preloadCache,
    syncPendingBatch,
  } = useScannerStore();

  // Animated laser scanline
  const scanLineAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    refreshStats();

    // Start scanner line loop animation
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(scanLineAnim, {
          toValue: 1,
          duration: 2000,
          useNativeDriver: true,
        }),
        Animated.timing(scanLineAnim, {
          toValue: 0,
          duration: 2000,
          useNativeDriver: true,
        }),
      ])
    );
    animation.start();

    return () => animation.stop();
  }, []);

  const handleBarcodeScanned = async (scanningResult: BarcodeScanningResult) => {
    const rawData = scanningResult.data;
    if (isScannedLocked || !rawData) return;

    // Lock scanning temporarily to prevent multiple reads
    setIsScannedLocked(true);

    try {
      await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Heavy);
      await processQrData(rawData);
    } catch (err: any) {
      console.error('[Scanner] Scan handling error:', err);
      Alert.alert('Lỗi quét mã', err.message);
    } finally {
      // Re-enable scanner after cooldown
      setTimeout(() => {
        setIsScannedLocked(false);
      }, 1500);
    }
  };

  const processQrData = async (qrString: string) => {
    // 1. Verify digital signature offline using cached Public Key
    const verification = await QrVerifier.verifyQrCode(qrString);

    if (!verification.isValid && !verification.publicBookingId) {
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
      Alert.alert(
        'QR Không Hợp Lệ',
        verification.error || 'Mã QR không đúng định dạng của Smart Waterbus.'
      );
      return;
    }

    const publicBookingId = verification.publicBookingId || qrString.trim();

    // 2. Query SQLite local cache for booking & tickets
    let booking = await SqliteRepository.getBookingByPublicId(publicBookingId);

    // If not found by publicId, fallback search by code
    if (!booking) {
      booking = await SqliteRepository.searchBookingOrTicket(publicBookingId);
    }

    if (booking) {
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      setCurrentBooking(booking, verification);
    } else {
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning);
      Alert.alert(
        'Chưa có dữ liệu chuyến trong máy',
        `Đã giải mã QR: "${publicBookingId}".\nTuy nhiên dữ liệu đơn này chưa có trong bộ nhớ SQLite của máy.\n\nNhân viên vui lòng bấm "Tải trước Cache" trước giờ mở cổng bến.`,
        [
          { text: 'Huỷ', style: 'cancel' },
          { text: 'Tải trước ngay', onPress: () => preloadCache() },
        ]
      );
    }
  };

  const handleManualSearch = async () => {
    if (!manualCode.trim()) return;
    setManualModalOpen(false);
    await processQrData(manualCode.trim());
    setManualCode('');
  };

  const triggerTestQrScan = (publicBookingId: string) => {
    setTestModalOpen(false);
    const test = QrVerifier.generateTestSignedQr(publicBookingId);
    processQrData(test.compactQr);
  };

  // Render Camera Permission fallback
  if (!permission) {
    return <View style={styles.centerContainer}><Text>Đang yêu cầu quyền camera...</Text></View>;
  }

  if (!permission.granted) {
    return (
      <View style={styles.centerContainer}>
        <Ionicons name="camera-reverse-outline" size={64} color="#0284C7" />
        <Text style={styles.permissionTitle}>Cần Quyền Truy Cập Camera</Text>
        <Text style={styles.permissionDesc}>
          Ứng dụng cần quyền camera để nhân viên soát vé quét mã QR lên tàu với tốc độ cao.
        </Text>
        <TouchableOpacity style={styles.permissionButton} onPress={requestPermission}>
          <Text style={styles.permissionButtonText}>Cấp Quyền Camera</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.permissionButton, { backgroundColor: '#0F172A', marginTop: 12 }]}
          onPress={() => setTestModalOpen(true)}
        >
          <Text style={styles.permissionButtonText}>Dùng Trình Giả Lập Quét (Demo)</Text>
        </TouchableOpacity>
      </View>
    );
  }

  const translateY = scanLineAnim.interpolate({
    inputRange: [0, 1],
    outputRange: [0, SCAN_AREA_SIZE - 4],
  });

  return (
    <View style={styles.container}>
      {/* Real Camera Viewfinder */}
      {Platform.OS !== 'web' ? (
        <CameraView
          style={StyleSheet.absoluteFill}
          facing={facing}
          enableTorch={torch}
          barcodeScannerSettings={{
            barcodeTypes: ['qr', 'code128', 'code39'],
          }}
          onBarcodeScanned={isScannedLocked ? undefined : handleBarcodeScanned}
        />
      ) : (
        <View style={[StyleSheet.absoluteFill, { backgroundColor: '#0F172A' }]} />
      )}

      {/* Top Header Controls Overlay */}
      <View style={styles.topOverlay}>
        <View style={styles.topInfoBar}>
          <View>
            <Text style={styles.staffNameText}>NV: {deviceInfo.staffName} ({deviceInfo.deviceCode})</Text>
            <Text style={styles.stationNameText}>{deviceInfo.stationName}</Text>
          </View>

          {stats.pendingSyncCount > 0 ? (
            <TouchableOpacity onPress={syncPendingBatch} style={styles.pendingBadge}>
              <Ionicons name="cloud-upload" size={16} color="#FFFFFF" />
              <Text style={styles.pendingBadgeText}>{stats.pendingSyncCount} chờ sync</Text>
            </TouchableOpacity>
          ) : (
            <View style={styles.syncedBadge}>
              <Ionicons name="checkmark-circle" size={16} color="#10B981" />
              <Text style={styles.syncedBadgeText}>Đã sync đủ</Text>
            </View>
          )}
        </View>

        {deviceInfo.forceOfflineMode && (
          <View style={styles.offlineBanner}>
            <Ionicons name="wifi-outline" size={16} color="#DC2626" />
            <Text style={styles.offlineBannerText}>ĐANG BẬT CHẾ ĐỘ NGOẠI TUYẾN CỐ ĐỊNH (OFFLINE FIRST)</Text>
          </View>
        )}
      </View>

      {/* Center Reticle Scanning Area */}
      <View style={styles.centerReticleContainer}>
        <View style={styles.reticle}>
          {/* 4 Corner Crosshairs */}
          <View style={[styles.corner, styles.cornerTL]} />
          <View style={[styles.corner, styles.cornerTR]} />
          <View style={[styles.corner, styles.cornerBL]} />
          <View style={[styles.corner, styles.cornerBR]} />

          {/* Animated Laser Scanline */}
          <Animated.View style={[styles.scanLine, { transform: [{ translateY }] }]} />
        </View>
        <Text style={styles.hintText}>Hướng camera vào mã QR vé trên điện thoại của khách</Text>
      </View>

      {/* Bottom Floating Control Buttons */}
      <View style={styles.bottomOverlay}>
        <View style={styles.controlRow}>
          {/* Torch / Flashlight Toggle */}
          <TouchableOpacity
            style={[styles.iconButton, torch && styles.iconButtonActive]}
            onPress={() => setTorch(!torch)}
          >
            <Ionicons name={torch ? 'flash' : 'flash-off'} size={24} color="#FFFFFF" />
            <Text style={styles.iconButtonText}>{torch ? 'Tắt Đèn' : 'Bật Đèn'}</Text>
          </TouchableOpacity>

          {/* Manual Code Input Fallback */}
          <TouchableOpacity
            style={styles.iconButton}
            onPress={() => setManualModalOpen(true)}
          >
            <Ionicons name="keypad" size={24} color="#FFFFFF" />
            <Text style={styles.iconButtonText}>Nhập Mã</Text>
          </TouchableOpacity>

          {/* Test Signed QR Simulator */}
          <TouchableOpacity
            style={[styles.iconButton, { backgroundColor: 'rgba(20, 122, 126, 0.85)' }]}
            onPress={() => setTestModalOpen(true)}
          >
            <Ionicons name="qr-code" size={24} color="#FFFFFF" />
            <Text style={styles.iconButtonText}>Test QR</Text>
          </TouchableOpacity>

          {/* Switch Camera Facing */}
          <TouchableOpacity
            style={styles.iconButton}
            onPress={() => setFacing(facing === 'back' ? 'front' : 'back')}
          >
            <Ionicons name="camera-reverse" size={24} color="#FFFFFF" />
            <Text style={styles.iconButtonText}>Đổi Cam</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Manual Input Modal */}
      <Modal visible={manualModalOpen} transparent animationType="fade">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalBox}>
            <Text style={styles.modalTitle}>Nhập Mã Vé / Booking Thủ Công</Text>
            <Text style={styles.modalDesc}>
              Dùng khi màn hình khách bị vỡ hoặc camera không nhận dạng được mã QR.
            </Text>
            <TextInput
              style={styles.input}
              placeholder="VD: pub_wb_98a76b5c hoặc WB20261006001"
              value={manualCode}
              onChangeText={setManualCode}
              autoCapitalize="none"
              autoCorrect={false}
            />
            <View style={styles.modalBtnRow}>
              <TouchableOpacity
                style={[styles.modalBtn, { backgroundColor: '#64748B' }]}
                onPress={() => setManualModalOpen(false)}
              >
                <Text style={styles.modalBtnText}>Huỷ</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.modalBtn, { backgroundColor: BrandColors.tealFlow }]}
                onPress={handleManualSearch}
              >
                <Text style={styles.modalBtnText}>Tra Cứu</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* Test QR Simulator Modal */}
      <Modal visible={testModalOpen} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalBox}>
            <Text style={styles.modalTitle}>Mô Phỏng Quét QR Vé Mẫu (Test)</Text>
            <Text style={styles.modalDesc}>
              Chọn một vé đã được ký số bằng Public Key để thử nghiệm xác thực ngoại tuyến:
            </Text>

            <TouchableOpacity
              style={styles.testItem}
              onPress={() => triggerTestQrScan('pub_wb_98a76b5c')}
            >
              <Text style={styles.testItemTitle}>Booking 1: Trần Văn Minh (3 vé: A01, A02, A03)</Text>
              <Text style={styles.testItemSubtitle}>ID: pub_wb_98a76b5c • Tuyến Bạch Đằng - Linh Đông</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.testItem}
              onPress={() => triggerTestQrScan('pub_wb_11f22e33')}
            >
              <Text style={styles.testItemTitle}>Booking 2: Nguyễn Thu Trang (2 vé: B05, B06)</Text>
              <Text style={styles.testItemSubtitle}>ID: pub_wb_11f22e33 • Khoang Tiêu Chuẩn</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.testItem}
              onPress={() => triggerTestQrScan('pub_wb_44d55c66')}
            >
              <Text style={styles.testItemTitle}>Booking 3: Phạm Đức Huy (1 vé: C10)</Text>
              <Text style={styles.testItemSubtitle}>ID: pub_wb_44d55c66 • Boong ngoài trời</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.modalBtn, { backgroundColor: '#0F172A', marginTop: 14 }]}
              onPress={() => setTestModalOpen(false)}
            >
              <Text style={styles.modalBtnText}>Đóng</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      {/* Check-In Passenger Detail Modal */}
      <BookingDetailModal />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000000',
  },
  centerContainer: {
    flex: 1,
    backgroundColor: BrandColors.mist,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  permissionTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: BrandColors.ink,
    marginTop: 16,
  },
  permissionDesc: {
    fontSize: 14,
    color: BrandColors.textSecondary,
    textAlign: 'center',
    marginVertical: 12,
    lineHeight: 20,
  },
  permissionButton: {
    backgroundColor: BrandColors.tealFlow,
    paddingVertical: 12,
    paddingHorizontal: 24,
    borderRadius: Radius.button,
    marginTop: 8,
  },
  permissionButtonText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 15,
  },
  topOverlay: {
    position: 'absolute',
    top: 50,
    left: 16,
    right: 16,
    zIndex: 10,
  },
  topInfoBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(13, 37, 56, 0.92)',
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: 'rgba(79, 195, 216, 0.25)',
  },
  staffNameText: {
    color: '#F8FAFC',
    fontSize: 14,
    fontWeight: '700',
  },
  stationNameText: {
    color: BrandColors.skyAqua,
    fontSize: 12,
    marginTop: 2,
    fontWeight: '600',
  },
  pendingBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: BrandColors.signalAmber,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: Radius.pill,
    gap: 4,
  },
  pendingBadgeText: {
    color: BrandColors.ink,
    fontSize: 12,
    fontWeight: '700',
  },
  syncedBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(5, 150, 105, 0.2)',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: Radius.pill,
    gap: 4,
  },
  syncedBadgeText: {
    color: '#34D399',
    fontSize: 12,
    fontWeight: '600',
  },
  offlineBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: BrandColors.dangerBg,
    paddingVertical: 6,
    borderRadius: Radius.sm,
    marginTop: 8,
    gap: 6,
  },
  offlineBannerText: {
    color: BrandColors.danger,
    fontSize: 11,
    fontWeight: '700',
  },
  centerReticleContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  reticle: {
    width: SCAN_AREA_SIZE,
    height: SCAN_AREA_SIZE,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.3)',
    backgroundColor: 'transparent',
    overflow: 'hidden',
  },
  corner: {
    position: 'absolute',
    width: 28,
    height: 28,
    borderColor: BrandColors.skyAqua,
  },
  cornerTL: {
    top: 0,
    left: 0,
    borderTopWidth: 4,
    borderLeftWidth: 4,
    borderTopLeftRadius: 14,
  },
  cornerTR: {
    top: 0,
    right: 0,
    borderTopWidth: 4,
    borderRightWidth: 4,
    borderTopRightRadius: 14,
  },
  cornerBL: {
    bottom: 0,
    left: 0,
    borderBottomWidth: 4,
    borderLeftWidth: 4,
    borderBottomLeftRadius: 14,
  },
  cornerBR: {
    bottom: 0,
    right: 0,
    borderBottomWidth: 4,
    borderRightWidth: 4,
    borderBottomRightRadius: 14,
  },
  scanLine: {
    width: '100%',
    height: 3,
    backgroundColor: BrandColors.skyAqua,
    shadowColor: BrandColors.skyAqua,
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.9,
    shadowRadius: 8,
  },
  hintText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '500',
    marginTop: 20,
    backgroundColor: 'rgba(13, 37, 56, 0.85)',
    paddingHorizontal: 16,
    paddingVertical: 6,
    borderRadius: Radius.pill,
    textAlign: 'center',
  },
  bottomOverlay: {
    position: 'absolute',
    bottom: 34,
    left: 16,
    right: 16,
  },
  controlRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    backgroundColor: 'rgba(13, 37, 56, 0.92)',
    paddingVertical: 12,
    borderRadius: Radius.card,
    borderWidth: 1,
    borderColor: 'rgba(79, 195, 216, 0.25)',
  },
  iconButton: {
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: Radius.sm,
  },
  iconButtonActive: {
    backgroundColor: 'rgba(232, 182, 62, 0.35)',
  },
  iconButtonText: {
    color: '#FFFFFF',
    fontSize: 11,
    marginTop: 4,
    fontWeight: '600',
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(13, 37, 56, 0.75)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  modalBox: {
    backgroundColor: '#FFFFFF',
    borderRadius: Radius.card,
    padding: 20,
    width: '100%',
    maxWidth: 400,
  },
  modalTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: BrandColors.ink,
    marginBottom: 6,
  },
  modalDesc: {
    fontSize: 13,
    color: BrandColors.textSecondary,
    marginBottom: 16,
  },
  input: {
    borderWidth: 1.5,
    borderColor: BrandColors.border,
    borderRadius: Radius.sm,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    marginBottom: 16,
  },
  modalBtnRow: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 10,
  },
  modalBtn: {
    paddingHorizontal: 18,
    paddingVertical: 10,
    borderRadius: Radius.button,
    alignItems: 'center',
  },
  modalBtnText: {
    color: '#FFFFFF',
    fontWeight: '700',
    fontSize: 14,
  },
  testItem: {
    backgroundColor: BrandColors.sandLight,
    padding: 12,
    borderRadius: Radius.sm,
    marginBottom: 10,
    borderLeftWidth: 4,
    borderLeftColor: BrandColors.tealFlow,
  },
  testItemTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: BrandColors.ink,
  },
  testItemSubtitle: {
    fontSize: 12,
    color: BrandColors.textSecondary,
    marginTop: 2,
  },
});

