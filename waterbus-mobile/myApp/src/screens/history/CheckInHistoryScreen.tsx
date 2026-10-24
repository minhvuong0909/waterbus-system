/**
 * Check-In History & Batch Sync Queue Screen
 * SMART WATERBUS - STAFF SCANNER (Tuấn - Thành viên 4)
 * Displays all local offline/online check-in events, sync queue status,
 * deduplication keys (client_event_id), and manual batch sync trigger.
 */

import React, { useState, useEffect, useCallback } from 'react';
import {
  StyleSheet,
  Text,
  View,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  TextInput,
  ActivityIndicator,
  Alert,
  Platform,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { SqliteRepository } from '../../services/sqlite/sqliteRepository';
import { useScannerStore } from '../../store/useScannerStore';
import { CheckInEvent } from '../../types/scanner';
import { BrandColors, Radius } from '../../../constants/theme';

export default function CheckInHistoryScreen() {
  const { stats, isSyncing, syncPendingBatch, refreshStats } = useScannerStore();
  const [events, setEvents] = useState<CheckInEvent[]>([]);
  const [filter, setFilter] = useState<'ALL' | 'PENDING' | 'SYNCED'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [refreshing, setRefreshing] = useState(false);

  const loadEvents = useCallback(async () => {
    try {
      const data = await SqliteRepository.getAllCheckInEvents(150);
      setEvents(data);
      await refreshStats();
    } catch (err: any) {
      console.error('[History] Failed to load check-in events:', err);
    }
  }, [refreshStats]);

  useEffect(() => {
    loadEvents();
  }, [loadEvents]);

  const onRefresh = async () => {
    setRefreshing(true);
    await loadEvents();
    setRefreshing(false);
  };

  const handleSyncNow = async () => {
    await Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    const count = await syncPendingBatch();
    await loadEvents();
    if (count > 0) {
      await Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      Alert.alert('Đồng Bộ Thành Công', `Đã đẩy lô ${count} sự kiện check-in lên máy chủ!`);
    } else {
      Alert.alert('Thông Báo', 'Không có sự kiện nào đang chờ đồng bộ.');
    }
  };

  const filteredEvents = events.filter((e) => {
    if (filter === 'PENDING' && e.syncStatus !== 'Pending') return false;
    if (filter === 'SYNCED' && e.syncStatus !== 'Synced') return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase().trim();
      const nameMatch = e.passengerName.toLowerCase().includes(q);
      const seatMatch = e.seatCode.toLowerCase().includes(q);
      const ticketMatch = e.ticketNumber.toLowerCase().includes(q);
      const idMatch = e.clientEventId.toLowerCase().includes(q);
      return nameMatch || seatMatch || ticketMatch || idMatch;
    }
    return true;
  });

  const pendingCount = events.filter((e) => e.syncStatus === 'Pending').length;
  const syncedCount = events.filter((e) => e.syncStatus === 'Synced').length;

  const renderEventItem = ({ item }: { item: CheckInEvent }) => {
    const isSynced = item.syncStatus === 'Synced';
    const dateFormatted = new Date(item.occurredAtDevice).toLocaleTimeString('vi-VN', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      day: '2-digit',
      month: '2-digit',
    });

    return (
      <View style={styles.card}>
        <View style={styles.cardHeader}>
          <View style={styles.cardHeaderLeft}>
            <View style={styles.seatBadge}>
              <Text style={styles.seatBadgeText}>Ghế {item.seatCode}</Text>
            </View>
            <Text style={styles.passengerName} numberOfLines={1}>
              {item.passengerName}
            </Text>
          </View>
          <View style={[styles.statusBadge, isSynced ? styles.badgeSynced : styles.badgePending]}>
            <Ionicons
              name={isSynced ? 'checkmark-circle' : 'cloud-upload'}
              size={13}
              color={isSynced ? '#059669' : '#D97706'}
            />
            <Text style={[styles.statusBadgeText, isSynced ? styles.textSynced : styles.textPending]}>
              {isSynced ? 'Đã Đồng Bộ' : 'Chờ Sync'}
            </Text>
          </View>
        </View>

        <View style={styles.divider} />

        <View style={styles.cardBody}>
          <View style={styles.infoRow}>
            <Ionicons name="ticket-outline" size={14} color="#64748B" />
            <Text style={styles.infoLabel}>Mã vé:</Text>
            <Text style={styles.infoValueBold}>{item.ticketNumber}</Text>
          </View>

          <View style={styles.infoRow}>
            <Ionicons name="time-outline" size={14} color="#64748B" />
            <Text style={styles.infoLabel}>Quét lúc:</Text>
            <Text style={styles.infoValue}>{dateFormatted}</Text>
          </View>

          <View style={styles.infoRow}>
            <Ionicons name="finger-print-outline" size={14} color="#64748B" />
            <Text style={styles.infoLabel}>Mã Event (UUID):</Text>
            <Text style={styles.uuidText} numberOfLines={1} ellipsizeMode="middle">
              {item.clientEventId}
            </Text>
          </View>

          {item.notes ? (
            <View style={styles.notesRow}>
              <Text style={styles.notesText}>Ghi chú: {item.notes}</Text>
            </View>
          ) : null}
        </View>
      </View>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      {/* Top Banner */}
      <View style={styles.header}>
        <View>
          <Text style={styles.headerTitle}>Lịch Sử Soát Vé</Text>
          <Text style={styles.headerSubtitle}>Hàng đợi đồng bộ lũy đẳng (Idempotent Sync)</Text>
        </View>
        <TouchableOpacity
          style={[styles.syncButton, isSyncing && styles.syncButtonDisabled]}
          onPress={handleSyncNow}
          disabled={isSyncing}
        >
          {isSyncing ? (
            <ActivityIndicator size="small" color="#FFFFFF" />
          ) : (
            <>
              <Ionicons name="sync" size={16} color="#FFFFFF" />
              <Text style={styles.syncButtonText}>Sync Lô</Text>
            </>
          )}
        </TouchableOpacity>
      </View>

      {/* Metrics Row */}
      <View style={styles.metricsContainer}>
        <View style={styles.metricCard}>
          <Text style={styles.metricNumber}>{events.length}</Text>
          <Text style={styles.metricLabel}>Tổng Đã Quét</Text>
        </View>
        <View style={[styles.metricCard, { borderTopColor: '#D97706', borderTopWidth: 3 }]}>
          <Text style={[styles.metricNumber, { color: '#D97706' }]}>{pendingCount}</Text>
          <Text style={styles.metricLabel}>Chờ Sync Server</Text>
        </View>
        <View style={[styles.metricCard, { borderTopColor: '#059669', borderTopWidth: 3 }]}>
          <Text style={[styles.metricNumber, { color: '#059669' }]}>{syncedCount}</Text>
          <Text style={styles.metricLabel}>Đã Lưu Cloud</Text>
        </View>
      </View>

      {/* Search Bar */}
      <View style={styles.searchContainer}>
        <Ionicons name="search" size={18} color="#94A3B8" style={{ marginRight: 8 }} />
        <TextInput
          style={styles.searchInput}
          placeholder="Tìm theo tên khách, mã vé, số ghế..."
          value={searchQuery}
          onChangeText={setSearchQuery}
          clearButtonMode="while-editing"
        />
        {searchQuery.length > 0 && (
          <TouchableOpacity onPress={() => setSearchQuery('')}>
            <Ionicons name="close-circle" size={18} color="#94A3B8" />
          </TouchableOpacity>
        )}
      </View>

      {/* Filter Tabs */}
      <View style={styles.filterRow}>
        <TouchableOpacity
          style={[styles.filterTab, filter === 'ALL' && styles.filterTabActive]}
          onPress={() => setFilter('ALL')}
        >
          <Text style={[styles.filterTabText, filter === 'ALL' && styles.filterTabTextActive]}>
            Tất Cả ({events.length})
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.filterTab, filter === 'PENDING' && styles.filterTabActive]}
          onPress={() => setFilter('PENDING')}
        >
          <Text style={[styles.filterTabText, filter === 'PENDING' && styles.filterTabTextActive]}>
            Chờ Sync ({pendingCount})
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.filterTab, filter === 'SYNCED' && styles.filterTabActive]}
          onPress={() => setFilter('SYNCED')}
        >
          <Text style={[styles.filterTabText, filter === 'SYNCED' && styles.filterTabTextActive]}>
            Đã Sync ({syncedCount})
          </Text>
        </TouchableOpacity>
      </View>

      {/* Event List */}
      <FlatList
        data={filteredEvents}
        keyExtractor={(item) => item.clientEventId || item.id}
        renderItem={renderEventItem}
        contentContainerStyle={styles.listContent}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Ionicons name="file-tray-outline" size={56} color="#CBD5E1" />
            <Text style={styles.emptyTitle}>Chưa có lượt soát vé nào</Text>
            <Text style={styles.emptySubtitle}>
              Khi nhân viên quét mã QR vé và xác nhận check-in, lịch sử và mã chống trùng lặp UUID sẽ xuất hiện tại đây.
            </Text>
          </View>
        }
      />
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
    paddingTop: 16,
    paddingBottom: 12,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: BrandColors.border,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: '800',
    color: BrandColors.ink,
  },
  headerSubtitle: {
    fontSize: 12,
    color: BrandColors.textSecondary,
    marginTop: 2,
  },
  syncButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: BrandColors.tealFlow,
    paddingVertical: 8,
    paddingHorizontal: 14,
    borderRadius: Radius.button,
    gap: 6,
  },
  syncButtonDisabled: {
    backgroundColor: BrandColors.textMuted,
  },
  syncButtonText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  metricsContainer: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 12,
    gap: 10,
  },
  metricCard: {
    flex: 1,
    backgroundColor: '#FFFFFF',
    paddingVertical: 12,
    paddingHorizontal: 8,
    borderRadius: Radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: BrandColors.border,
  },
  metricNumber: {
    fontSize: 20,
    fontWeight: '800',
    color: BrandColors.ink,
  },
  metricLabel: {
    fontSize: 11,
    color: BrandColors.textSecondary,
    marginTop: 3,
    textAlign: 'center',
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    marginHorizontal: 16,
    marginBottom: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: BrandColors.border,
  },
  searchInput: {
    flex: 1,
    fontSize: 14,
    color: BrandColors.ink,
  },
  filterRow: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    marginBottom: 8,
    gap: 8,
  },
  filterTab: {
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: Radius.pill,
    backgroundColor: BrandColors.sandLight,
  },
  filterTabActive: {
    backgroundColor: BrandColors.tealFlow,
  },
  filterTabText: {
    fontSize: 12,
    fontWeight: '600',
    color: BrandColors.ink,
  },
  filterTabTextActive: {
    color: '#FFFFFF',
  },
  listContent: {
    paddingHorizontal: 16,
    paddingBottom: 24,
  },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: Radius.card,
    padding: 14,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: BrandColors.border,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 1,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  cardHeaderLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
    marginRight: 8,
  },
  seatBadge: {
    backgroundColor: BrandColors.sandLight,
    paddingVertical: 3,
    paddingHorizontal: 8,
    borderRadius: Radius.sm,
    marginRight: 8,
  },
  seatBadgeText: {
    fontSize: 12,
    fontWeight: '700',
    color: BrandColors.tealFlow,
  },
  passengerName: {
    fontSize: 15,
    fontWeight: '700',
    color: BrandColors.ink,
    flex: 1,
  },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 3,
    paddingHorizontal: 8,
    borderRadius: Radius.pill,
    gap: 4,
  },
  badgeSynced: {
    backgroundColor: BrandColors.successBg,
  },
  badgePending: {
    backgroundColor: '#FEF3C7',
  },
  statusBadgeText: {
    fontSize: 11,
    fontWeight: '700',
  },
  textSynced: {
    color: BrandColors.success,
  },
  textPending: {
    color: BrandColors.signalAmber,
  },
  divider: {
    height: 1,
    backgroundColor: BrandColors.border,
    marginVertical: 10,
  },
  cardBody: {
    gap: 6,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  infoLabel: {
    fontSize: 12,
    color: BrandColors.textSecondary,
  },
  infoValueBold: {
    fontSize: 12,
    fontWeight: '700',
    color: BrandColors.ink,
  },
  infoValue: {
    fontSize: 12,
    color: BrandColors.ink,
  },
  uuidText: {
    fontSize: 11,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
    color: BrandColors.textSecondary,
    flex: 1,
  },
  notesRow: {
    marginTop: 4,
    paddingTop: 4,
    borderTopWidth: 1,
    borderTopColor: BrandColors.border,
  },
  notesText: {
    fontSize: 11,
    color: BrandColors.textMuted,
    fontStyle: 'italic',
  },
  emptyContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 60,
    paddingHorizontal: 32,
  },
  emptyTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: BrandColors.textSecondary,
    marginTop: 12,
  },
  emptySubtitle: {
    fontSize: 13,
    color: BrandColors.textMuted,
    textAlign: 'center',
    marginTop: 6,
    lineHeight: 18,
  },
});
