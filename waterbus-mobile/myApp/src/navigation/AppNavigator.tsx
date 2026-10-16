import React from 'react';
import { createNativeStackNavigator } from "@react-navigation/native-stack";
import HomeScreen from "../screens/HomeScreen";
import StaffScannerScreen from "../screens/scanner/StaffScannerScreen";
import PreloadManifestScreen from "../screens/manifest/PreloadManifestScreen";
import ScannerSettingsScreen from "../screens/settings/ScannerSettingsScreen";

const Stack = createNativeStackNavigator();

export default function AppNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="home" component={HomeScreen} />
      <Stack.Screen name="staff-scanner" component={StaffScannerScreen} />
      <Stack.Screen name="preload-manifest" component={PreloadManifestScreen} />
      <Stack.Screen name="scanner-settings" component={ScannerSettingsScreen} />
    </Stack.Navigator>
  );
}
