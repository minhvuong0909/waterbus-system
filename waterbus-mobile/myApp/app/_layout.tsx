import { useEffect } from "react";
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

const queryClient = new QueryClient();

export const unstable_settings = {
  anchor: "(tabs)",
};

export default function RootLayout() {
  const colorScheme = useColorScheme();
  const refreshStats = useScannerStore((s) => s.refreshStats);

  useEffect(() => {
    // Mở SQLite trước, sau đó đồng bộ số liệu cache lên trạng thái giao diện.
    getDatabase()
      .then(() => {
        refreshStats();
      })
      .catch(() => {});

    // Thử gửi các lượt check-in đang chờ khi ứng dụng còn chạy.
    syncService.startBackgroundWorker(15000);

    return () => {
      // Dừng bộ hẹn giờ khi layout bị tháo khỏi ứng dụng.
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
            options={{ presentation: "modal", title: "Tra cứu vé" }}
          />
        </Stack>
        <StatusBar style="auto" />
      </ThemeProvider>
    </QueryClientProvider>
  );
}
