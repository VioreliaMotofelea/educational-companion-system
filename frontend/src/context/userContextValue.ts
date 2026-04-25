import { createContext } from "react";

export type UserContextValue = {
  userId: string;
};

const DEFAULT_USER_ID = (
  import.meta.env.VITE_DEFAULT_USER_ID ??
  "user-1"
).trim();

export const UserContext = createContext<UserContextValue>({
  userId: DEFAULT_USER_ID,
});

export const defaultUserContextValue: UserContextValue = {
  userId: DEFAULT_USER_ID,
};
