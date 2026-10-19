import { Redirect } from 'expo-router';
import { useAuthStore } from '../src/store/useAuthStore';

/** Điểm vào khi mở app từ QR Expo hoặc deep link gốc. */
export default function Index() {
  const { user, isRestoring } = useAuthStore();
  if (isRestoring) return null;
  return <Redirect href={user ? '/(tabs)/scanner' : '/login'} />;
}
