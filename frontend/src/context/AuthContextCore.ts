import { createContext } from "react";

export type AuthUser = {
  userId: string;
  email: string;
};

export type RegisterPayload = {
  email: string;
  password: string;
  dailyAvailableMinutes: number;
};

export type LoginPayload = {
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
