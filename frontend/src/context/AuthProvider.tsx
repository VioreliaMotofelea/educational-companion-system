import { useEffect, useMemo, useState } from "react";
import { clearStoredAuthSession, getStoredAuthSession } from "../services/authStorage";
import { getCurrentUser, login, logout, register } from "../services/api";
import { AuthContext, type AuthContextValue, type AuthUser } from "./AuthContextCore";

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

    void hydrate();
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
