import { createContext, useEffect, useMemo, useState } from "react";
import { clearStoredAuthSession, getStoredAuthSession } from "../services/authStorage";
import { getCurrentUser, login, logout, register } from "../services/api";

type AuthUser = {
  userId: string;
  email: string;
};

type RegisterPayload = {
  email: string;
  password: string;
  dailyAvailableMinutes: number;
};

type LoginPayload = {
  email: string;
  password: string;
};

export type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  loading: boolean;
  login: (payload: LoginPayload) => Promise<void>;
  register: (payload: RegisterPayload) => Promise<void>;
  logout: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextValue>({
  user: null,
  isAuthenticated: false,
  loading: true,
  login: async () => {},
  register: async () => {},
  logout: async () => {},
});

type Props = {
  children: React.ReactNode;
};

export function AuthProvider({ children }: Props) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const hydrate = async () => {
      const stored = getStoredAuthSession();
      if (!stored) {
        setLoading(false);
        return;
      }

      try {
        const currentUser = await getCurrentUser();
        setUser({ userId: currentUser.userId, email: currentUser.email });
      } catch {
        clearStoredAuthSession();
        setUser(null);
      } finally {
        setLoading(false);
      }
    };

    hydrate();
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      loading,
      login: async (payload) => {
        const session = await login(payload);
        setUser({ userId: session.userId, email: session.email });
      },
      register: async (payload) => {
        const session = await register(payload);
        setUser({ userId: session.userId, email: session.email });
      },
      logout: async () => {
        try {
          await logout();
        } finally {
          clearStoredAuthSession();
          setUser(null);
        }
      },
    }),
    [loading, user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
