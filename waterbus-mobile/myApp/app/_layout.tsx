import React, { useEffect } from "react";
import {
  DarkTheme,
  DefaultTheme,
  ThemeProvider,
} from "@react-navigation/native";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import "react-native-reanimated";

import { useColorScheme } from "@/hooks/use-color-scheme";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { getDatabase } from "@/src/services/sqlite/database";
import { syncService } from "@/src/services/sync/syncService";
import { useScannerStore } from "@/src/store/useScannerStore";

const queryClient = new QueryClient();

export const unstable_settings = {
  anchor: "(tabs)",
};

export default function RootLayout() {
  const colorScheme = useColorScheme();
  const refreshStats = useScannerStore((s) => s.refreshStats);

  useEffect(() => {
    // 1. Initialize SQLite schema on boot
    getDatabase()
      .then(() => {
        console.log("[Waterbus] SQLite initialized successfully.");
        refreshStats();
      })
      .catch((err) => console.error("[Waterbus] DB Init failed:", err));

    // 2. Start idempotent batch sync background worker
    syncService.startBackgroundWorker(15000);

    return () => {
      syncService.stopBackgroundWorker();
    };
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider value={colorScheme === "dark" ? DarkTheme : DefaultTheme}>
        <Stack>
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          <Stack.Screen
            name="modal"
            options={{ presentation: "modal", title: "Modal" }}
          />
        </Stack>
        <StatusBar style="auto" />
      </ThemeProvider>
    </QueryClientProvider>
  );
}
