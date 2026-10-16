import { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { Language } from '@/lib/i18n';
import { authApi, profileApi, tokenStore, ApiError, type AuthUser, type Profile } from '@/lib/api';

export type ThemeMode = 'system' | 'light' | 'dark';

export interface RegisterData {
  email: string;
  password: string;
  fullName: string;
  acceptTerms: boolean;
  referralCode?: string;
}

interface AppContextType {
  language: Language;
  setLanguage: (lang: Language) => void;
  /** Hozir qorong'i tema ko'rsatilyaptimi (theme + tizim sozlamasi) */
  darkMode: boolean;
  setDarkMode: (v: boolean) => void;
  /** system — qurilma sozlamasiga qarab (standart) */
  theme: ThemeMode;
  setTheme: (mode: ThemeMode) => void;
  user: AuthUser | null;
  profile: Profile | null;
  loading: boolean;
  refreshProfile: () => Promise<void>;
  /** /api/auth/me dan foydalanuvchini qayta yuklaydi (masalan, email tasdiqlangandan keyin) */
  refreshUser: () => Promise<void>;
  login: (email: string, password: string) => Promise<AuthUser>;
  register: (data: RegisterData) => Promise<AuthUser>;
  logout: () => void;
}

const AppContext = createContext<AppContextType>({
  language: 'uz',
  setLanguage: () => {},
  darkMode: true,
  setDarkMode: () => {},
  theme: 'system',
  setTheme: () => {},
  user: null,
  profile: null,
  loading: true,
  refreshProfile: async () => {},
  refreshUser: async () => {},
  login: async () => { throw new Error('not initialized'); },
  register: async () => { throw new Error('not initialized'); },
  logout: () => {},
});

export const useApp = () => useContext(AppContext);

function prefersDark(): boolean {
  return typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-color-scheme: dark)').matches;
}

function readTheme(): ThemeMode {
  try {
    const saved = localStorage.getItem('dm-theme');
    if (saved === 'light' || saved === 'dark' || saved === 'system') return saved;
    // Eski sozlama (dm-dark) — foydalanuvchi o'zi tanlagan bo'lsa saqlanadi
    const legacy = localStorage.getItem('dm-dark');
    if (legacy !== null) return legacy === 'true' ? 'dark' : 'light';
  } catch { /* localStorage yopiq */ }
  return 'system';
}

const normalizeUser = (authUser: AuthUser): AuthUser => ({
  ...authUser,
  roles: (authUser.roles ?? []).map(role => role.toLowerCase()),
});

export const AppProvider = ({ children }: { children: ReactNode }) => {
  const [language, setLanguageState] = useState<Language>(() => {
    return (localStorage.getItem('dm-lang') as Language) || 'uz';
  });
  const [theme, setThemeState] = useState<ThemeMode>(readTheme);
  const [systemDark, setSystemDark] = useState(prefersDark);
  const darkMode = theme === 'dark' || (theme === 'system' && systemDark);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);

  const setLanguage = (lang: Language) => {
    setLanguageState(lang);
    localStorage.setItem('dm-lang', lang);
  };

  const setTheme = (mode: ThemeMode) => {
    setThemeState(mode);
    try {
      localStorage.setItem('dm-theme', mode);
      localStorage.removeItem('dm-dark');
    } catch { /* saqlab bo'lmasa — faqat shu sessiya */ }
  };
  const setDarkMode = (v: boolean) => setTheme(v ? 'dark' : 'light');

  // Qurilma temasi o'zgarsa (masalan, kechqurun avtomatik qorong'i) — darhol moslashamiz
  useEffect(() => {
    const mq = window.matchMedia?.('(prefers-color-scheme: dark)');
    if (!mq) return;
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);

  useEffect(() => {
    document.documentElement.classList.toggle('dark', darkMode);
    document.documentElement.style.colorScheme = darkMode ? 'dark' : 'light';
  }, [darkMode]);

  const fetchProfile = useCallback(async () => {
    try {
      const prof = await profileApi.me();
      // Banned foydalanuvchini chiqarib yuborish
      if (prof.isBanned) {
        tokenStore.clear();
        setUser(null);
        setProfile(null);
        return;
      }
      setProfile(prof);
    } catch (err) {
      // Token yaroqsiz bo'lsa — chiqishga majbur
      if (err instanceof ApiError && err.status === 401) {
        tokenStore.clear();
        setUser(null);
        setProfile(null);
      } else {
        console.error('Profile fetch error:', err);
      }
    }
  }, []);

  const refreshProfile = useCallback(async () => {
    if (tokenStore.get()) await fetchProfile();
  }, [fetchProfile]);

  // Sahifa yuklanganda token bor bo'lsa — foydalanuvchini qayta tiklaymiz
  useEffect(() => {
    const init = async () => {
      const token = tokenStore.get();
      if (!token) {
        setLoading(false);
        return;
      }
      try {
        const me = normalizeUser(await authApi.me());
        setUser(me);
        await fetchProfile();
      } catch {
        tokenStore.clear();
        setUser(null);
        setProfile(null);
      } finally {
        setLoading(false);
      }
    };
    init();
  }, [fetchProfile]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login({ email, password });
    const authUser = normalizeUser(res.user);
    tokenStore.set(res.accessToken, res.expiresAt);
    setUser(authUser);
    // Profilni to'liq yuklaymiz (yums, avatar, phone va h.k.)
    try {
      const prof = await profileApi.me();
      if (prof.isBanned) {
        tokenStore.clear();
        setUser(null);
        setProfile(null);
        throw new Error('Sizning akkauntingiz bloklangan.');
      }
      setProfile(prof);
    } catch (err) {
      if (err instanceof Error && err.message.includes('bloklangan')) throw err;
      console.error('Profile load after login:', err);
    }
    return authUser;
  }, []);

  const register = useCallback(async (data: RegisterData) => {
    const res = await authApi.register(data);
    const authUser = normalizeUser(res.user);
    tokenStore.set(res.accessToken, res.expiresAt);
    setUser(authUser);
    try {
      const prof = await profileApi.me();
      setProfile(prof);
    } catch (err) {
      console.error('Profile load after register:', err);
    }
    return authUser;
  }, []);

  const refreshUser = useCallback(async () => {
    if (!tokenStore.get()) return;
    try {
      setUser(normalizeUser(await authApi.me()));
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        tokenStore.clear();
        setUser(null);
        setProfile(null);
      }
    }
  }, []);

  const logout = useCallback(() => {
    tokenStore.clear();
    setUser(null);
    setProfile(null);
  }, []);

  return (
    <AppContext.Provider
      value={{
        language,
        setLanguage,
        darkMode,
        setDarkMode,
        theme,
        setTheme,
        user,
        profile,
        loading,
        refreshProfile,
        refreshUser,
        login,
        register,
        logout,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};
