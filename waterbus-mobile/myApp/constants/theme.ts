/**
 * Smart Waterbus Official Design System Tokens
 * Source of Truth: docs/stitch/stitch_smart_waterbus_passenger_web/smart_waterbus_design.md
 * Concept: Urban River Flow
 */

import { Platform } from 'react-native';

export const BrandColors = {
  // Core colors
  deepRiver: '#0D2538',   // Dominant dark navy / operations header
  tealFlow: '#147A7E',    // Primary brand action & active state
  skyAqua: '#4FC3D8',     // High-visibility accent / scanline / laser
  mist: '#F4F7F8',        // Screen background
  sandLight: '#E9F0EC',   // Soft light river surface tone
  ink: '#18242D',         // High contrast primary text

  // Accent colors
  coralGlow: '#F08A6B',   // Warm alert & notification accent
  signalAmber: '#E8B63E', // Pending sync & caution warning

  // Utility & Status
  success: '#059669',
  successBg: '#DCFCE7',
  danger: '#DC2626',
  dangerBg: '#FEF2F2',
  surface: '#FFFFFF',
  cardBg: '#FFFFFF',
  border: '#E2E8F0',
  textSecondary: '#64748B',
  textMuted: '#94A3B8',
};

export const Radius = {
  sm: 8,
  md: 12,
  button: 14,
  card: 16,
  panel: 24,
  pill: 9999,
};

export const Colors = {
  light: {
    text: BrandColors.ink,
    background: BrandColors.mist,
    tint: BrandColors.tealFlow,
    icon: BrandColors.textSecondary,
    tabIconDefault: BrandColors.textSecondary,
    tabIconSelected: BrandColors.tealFlow,
  },
  dark: {
    text: '#F8FAFC',
    background: BrandColors.deepRiver,
    tint: BrandColors.skyAqua,
    icon: '#94A3B8',
    tabIconDefault: '#94A3B8',
    tabIconSelected: BrandColors.skyAqua,
  },
};

export const Fonts = Platform.select({
  ios: {
    sans: 'system-ui',
    serif: 'ui-serif',
    rounded: 'ui-rounded',
    mono: 'ui-monospace',
  },
  default: {
    sans: 'normal',
    serif: 'serif',
    rounded: 'normal',
    mono: 'monospace',
  },
  web: {
    sans: "Plus Jakarta Sans, Inter, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
    serif: "Georgia, 'Times New Roman', serif",
    rounded: "'SF Pro Rounded', 'Hiragino Maru Gothic ProN', Meiryo, sans-serif",
    mono: "SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', monospace",
  },
});
