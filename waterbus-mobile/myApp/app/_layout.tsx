import React, { useEffect, useState } from "react";
import { View, ActivityIndicator, Text, TouchableOpacity } from "react-native";
import {
  DarkTheme,
  DefaultTheme,
  ThemeProvider,
} from "expo-router/react-navigation";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import "react-native-reanimated";

import { useColorScheme } from "@/hooks/use-color-scheme";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { getDatabase } from "@/src/services/sqlite/database";
import { syncService } from "@/src/services/sync/syncService";
import { useScannerStore } from "@/src/store/useScannerStore";
import { useAuthStore } from "@/src/store/useAuthStore";

const queryClient = new QueryClient();

export default function RootLayout() {
  const colorScheme = useColorScheme();
  const refreshStats = useScannerStore((s) => s.refreshStats);
  const { user, isRestoring, restoreSession } = useAuthStore();
  // MOB-01: SQLite native mở lỗi phải khoá hẳn ứng dụng (không cho soát vé trên bộ nhớ
  // không bền), không chỉ log console rồi để app chạy tiếp như không có gì xảy ra.
  const [dbFatalError, setDbFatalError] = useState<string | null>(null);
  const [isInitializingDb, setIsInitializingDb] = useState(true);

  const loadDatabase = () => {
    getDatabase()
      .then(() => {
        console.log("[Waterbus] SQLite initialized successfully.");
        setIsInitializingDb(false);
        refreshStats();
      })
      .catch((err) => {
        console.error("[Waterbus] DB Init failed:", err);
        setIsInitializingDb(false);
        setDbFatalError(err?.message || "Không thể mở bộ nhớ offline trên thiết bị.");
      });
  };

  // Nút "Thử lại" trên màn hình lỗi (event handler, không phải effect) - được phép setState
  // đồng bộ trước khi gọi lại loadDatabase().
  const handleRetryDb = () => {
    setIsInitializingDb(true);
    setDbFatalError(null);
    loadDatabase();
  };

  useEffect(() => {
    // 1. Initialize SQLite schema on boot
    loadDatabase();

    // 2. Start idempotent batch sync background worker

    // 3. Khôi phục phiên đăng nhập Staff đã lưu (nếu có) trước khi quyết định route đầu tiên
    restoreSession();

    return () => {
      syncService.stopBackgroundWorker();
    };
  }, []);

  useEffect(() => {
    if (user && !isRestoring) syncService.startBackgroundWorker(15000);
    else syncService.stopBackgroundWorker();
    const unsubscribe = syncService.subscribe((status) => {
      useScannerStore.setState({ isSyncing: status.isSyncing, syncMessage: status.lastMessage });
      refreshStats();
    });
    return () => { unsubscribe(); syncService.stopBackgroundWorker(); };
  }, [user, isRestoring]);

  if (dbFatalError) {
    return (
      <View style={{ flex: 1, justifyContent: "center", alignItems: "center", backgroundColor: "#450A0A", padding: 24 }}>
        <Text style={{ color: "#FCA5A5", fontSize: 18, fontWeight: "700", textAlign: "center", marginBottom: 12 }}>
          Không thể khởi tạo bộ nhớ offline
        </Text>
        <Text style={{ color: "#FECACA", fontSize: 14, textAlign: "center", marginBottom: 24 }}>{dbFatalError}</Text>
        <TouchableOpacity
          onPress={handleRetryDb}
          style={{ backgroundColor: "#DC2626", paddingHorizontal: 24, paddingVertical: 12, borderRadius: 8 }}
        >
          <Text style={{ color: "#FFFFFF", fontWeight: "600" }}>Thử lại</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (isInitializingDb || isRestoring) {
    return (
      <View style={{ flex: 1, justifyContent: "center", alignItems: "center", backgroundColor: "#F8FAFC" }}>
        <ActivityIndicator size="large" color="#0284C7" />
      </View>
    );
  }

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider value={colorScheme === "dark" ? DarkTheme : DefaultTheme}>
        <Stack>
          <Stack.Screen name="index" options={{ headerShown: false }} />
          <Stack.Protected guard={!!user}>
            <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          </Stack.Protected>
          <Stack.Protected guard={!user}>
            <Stack.Screen name="login" options={{ headerShown: false }} />
          </Stack.Protected>
        </Stack>
        <StatusBar style="auto" />
      </ThemeProvider>
    </QueryClientProvider>
  );
}
