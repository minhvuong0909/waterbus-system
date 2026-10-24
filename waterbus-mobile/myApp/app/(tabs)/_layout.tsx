import { Tabs } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { Platform } from "react-native";
import { BrandColors } from "../../constants/theme";

export default function TabLayout() {
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: BrandColors.tealFlow,
        tabBarInactiveTintColor: BrandColors.textSecondary,
        tabBarStyle: {
          backgroundColor: "#FFFFFF",
          borderTopColor: BrandColors.border,
          borderTopWidth: 1,
          height: Platform.OS === "ios" ? 88 : 64,
          paddingBottom: Platform.OS === "ios" ? 28 : 10,
          paddingTop: 8,
        },
        tabBarLabelStyle: {
          fontSize: 11,
          fontWeight: "600",
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: "Soát Vé",
          tabBarIcon: ({ color, size, focused }) => (
            <Ionicons
              name={focused ? "qr-code" : "qr-code-outline"}
              size={size + 2}
              color={color}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="manifest"
        options={{
          title: "Chuyến & Tải Vé",
          tabBarIcon: ({ color, size, focused }) => (
            <Ionicons
              name={focused ? "boat" : "boat-outline"}
              size={size + 2}
              color={color}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="history"
        options={{
          title: "Lịch Sử",
          tabBarIcon: ({ color, size, focused }) => (
            <Ionicons
              name={focused ? "time" : "time-outline"}
              size={size + 2}
              color={color}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{
          title: "Ca Trực",
          tabBarIcon: ({ color, size, focused }) => (
            <Ionicons
              name={focused ? "person-circle" : "person-circle-outline"}
              size={size + 2}
              color={color}
            />
          ),
        }}
      />
    </Tabs>
  );
}
