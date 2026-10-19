/**
 * Màn hình đăng nhập Staff Scanner - gọi thật POST /api/v1/auth/login.
 * Chỉ tài khoản có vai trò Staff được vào app (useAuthStore.login từ chối vai trò khác).
 */
import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useAuthStore } from '../../store/useAuthStore';

export default function LoginScreen() {
  const { login, isLoggingIn, error } = useAuthStore();
  const [email, setEmail] = useState('staff@waterbus.vn');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);

  const handleLogin = async () => {
    if (!email.trim() || !password) return;
    await login(email.trim(), password);
  };

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={{ flex: 1 }}
      >
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <View style={styles.logoWrap}>
            <View style={styles.logoCircle}>
              <Ionicons name="boat" size={36} color="#FFFFFF" />
            </View>
            <Text style={styles.appName}>Smart Waterbus</Text>
            <Text style={styles.appSubtitle}>STAFF SCANNER · SOÁT VÉ TẠI BẾN</Text>
          </View>

          <View style={styles.card}>
            <Text style={styles.formTitle}>Chào mừng trở lại</Text>
            <Text style={styles.formSubtitle}>Đăng nhập để bắt đầu ca soát vé.</Text>
            <Text style={styles.inputLabel}>Email</Text>
            <TextInput
              style={styles.input}
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              keyboardType="email-address"
              placeholder="staff@waterbus.vn"
              editable={!isLoggingIn}
            />

            <Text style={styles.inputLabel}>Mật khẩu</Text>
            <View style={styles.passwordRow}>
              <TextInput
                style={[styles.input, { flex: 1, marginBottom: 0, borderWidth: 0 }]}
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPassword}
                placeholder="••••••••"
                editable={!isLoggingIn}
                onSubmitEditing={handleLogin}
              />
              <TouchableOpacity accessibilityRole="button" accessibilityLabel={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} style={styles.eyeBtn} onPress={() => setShowPassword((v) => !v)}>
                <Ionicons name={showPassword ? 'eye-off-outline' : 'eye-outline'} size={20} color="#64748B" />
              </TouchableOpacity>
            </View>

            {error ? (
              <View style={styles.errorBox}>
                <Ionicons name="alert-circle" size={16} color="#DC2626" />
                <Text style={styles.errorText}>{error}</Text>
              </View>
            ) : null}

            <TouchableOpacity
              style={[styles.loginBtn, (isLoggingIn || !email.trim() || !password) && styles.loginBtnDisabled]}
              onPress={handleLogin}
              disabled={isLoggingIn || !email.trim() || !password}
            >
              {isLoggingIn ? (
                <ActivityIndicator color="#FFFFFF" />
              ) : (
                <Text style={styles.loginBtnText}>Đăng nhập</Text>
              )}
            </TouchableOpacity>
          </View>

          <Text style={styles.hint}>
            Dành cho nhân viên soát vé Smart Waterbus
          </Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0B253B' },
  content: { flexGrow: 1, justifyContent: 'center', padding: 24, width: '100%', maxWidth: 480, alignSelf: 'center' },
  logoWrap: { alignItems: 'center', marginBottom: 32 },
  logoCircle: {
    width: 72,
    height: 72,
    borderRadius: 36,
    backgroundColor: '#0284C7',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 12,
  },
  appName: { fontSize: 27, fontWeight: '800', color: '#FFFFFF', letterSpacing: -0.7 },
  appSubtitle: { fontSize: 10, color: '#A5C4D5', marginTop: 8, letterSpacing: 1.2 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 24,
    padding: 24,
    borderWidth: 1,
    borderColor: '#E2E8F0',
  },
  formTitle: { fontSize: 23, fontWeight: '800', color: '#102A43', letterSpacing: -0.5 },
  formSubtitle: { color: '#64748B', fontSize: 13, marginTop: 6, marginBottom: 24 },
  inputLabel: { fontSize: 13, color: '#475569', marginBottom: 6, fontWeight: '600' },
  input: {
    borderWidth: 1.5,
    borderColor: '#CBD5E1',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    marginBottom: 16,
    backgroundColor: '#F8FAFC',
    color: '#0F172A',
  },
  passwordRow: { flexDirection: 'row', alignItems: 'center', borderWidth: 1.5, borderColor: '#CBD5E1', borderRadius: 10, backgroundColor: '#F8FAFC', marginBottom: 16 },
  eyeBtn: { padding: 12, minWidth: 44, minHeight: 48, alignItems: 'center', justifyContent: 'center' },
  errorBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FEF2F2',
    borderRadius: 8,
    padding: 10,
    marginBottom: 14,
    gap: 6,
  },
  errorText: { color: '#DC2626', fontSize: 12.5, flex: 1 },
  loginBtn: {
    backgroundColor: '#0284C7',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 4,
  },
  loginBtnDisabled: { backgroundColor: '#94A3B8' },
  loginBtnText: { color: '#FFFFFF', fontWeight: '700', fontSize: 15 },
  hint: { textAlign: 'center', color: '#A5C4D5', fontSize: 12, marginTop: 20, paddingHorizontal: 12 },
});
