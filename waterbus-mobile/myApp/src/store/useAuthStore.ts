import { create } from "zustand";

export interface AuthenticatedStaff {
  userId: string;
  email: string;
  fullName: string;
}

interface AuthState {
  user: AuthenticatedStaff | null;
  setUser: (user: AuthenticatedStaff) => void;
  clearUser: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  setUser: (user) => set({ user }),
  clearUser: () => set({ user: null }),
}));
