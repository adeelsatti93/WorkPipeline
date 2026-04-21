import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import api from '../api/client';

interface AuthState {
  token: string | null;
  username: string | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: () => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      username: null,
      login: async (username, password) => {
        const res = await api.post<{ token: string; username: string }>('/auth/login', { username, password });
        const { token, username: user } = res.data;
        localStorage.setItem('token', token);
        set({ token, username: user });
      },
      logout: () => {
        localStorage.removeItem('token');
        set({ token: null, username: null });
      },
      isAuthenticated: () => !!get().token,
    }),
    { name: 'auth-store', partialize: (s) => ({ token: s.token, username: s.username }) }
  )
);
