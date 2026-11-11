import React, { useEffect, useState } from "react";
import { View, ActivityIndicator, Text } from "react-native";
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
  const [databaseError, setDatabaseError] = useState<string | null>(null);

  useEffect(() => {
    getDatabase()
      .then(() => {
        refreshStats();
      })
      .catch((err) => {
        console.error("[Waterbus] DB Init failed:", err);
        setDatabaseError(err instanceof Error ? err.message : String(err));
      });

    // Khôi phục phiên Staff trước khi quyết định route đầu tiên.
    restoreSession();

    return () => {
      syncService.stopBackgroundWorker();
    };
  }, [refreshStats, restoreSession]);

  useEffect(() => {
    if (user && !isRestoring) syncService.startBackgroundWorker(15000);
    else syncService.stopBackgroundWorker();
    const unsubscribe = syncService.subscribe((status) => {
      useScannerStore.setState({ isSyncing: status.isSyncing, syncMessage: status.lastMessage });
      refreshStats();
    });
    return () => { unsubscribe(); syncService.stopBackgroundWorker(); };
  }, [user, isRestoring, refreshStats]);

  if (databaseError) {
    return (
      <View style={{ flex: 1, justifyContent: "center", padding: 24, backgroundColor: "#F8FAFC" }}>
        <Text style={{ color: "#991B1B", fontSize: 20, fontWeight: "700", marginBottom: 12 }}>
          Không thể khởi tạo cơ sở dữ liệu offline
        </Text>
        <Text style={{ color: "#334155", fontSize: 15, lineHeight: 22 }}>
          {databaseError}
          {"\n\n"}Dữ liệu hiện có không bị xoá. Không gỡ hoặc cài lại ứng dụng; vui lòng báo quản trị viên để xử lý.
        </Text>
      </View>
    );
  }

  if (isRestoring) {
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
