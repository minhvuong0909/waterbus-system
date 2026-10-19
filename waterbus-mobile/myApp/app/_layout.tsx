import React, { useEffect } from "react";
import { View, ActivityIndicator } from "react-native";
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

  useEffect(() => {
    // 1. Initialize SQLite schema on boot
    getDatabase()
      .then(() => {
        console.log("[Waterbus] SQLite initialized successfully.");
        refreshStats();
      })
      .catch((err) => console.error("[Waterbus] DB Init failed:", err));

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
