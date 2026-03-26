import { createContext } from "react";

export type UserContextValue = {
  userId: string;
};

const DEFAULT_USER_ID = (
  import.meta.env.VITE_DEFAULT_USER_ID ??
  "11111111-1111-1111-1111-111111111111"
).trim();

export const UserContext = createContext<UserContextValue>({
  userId: DEFAULT_USER_ID,
});

export const defaultUserContextValue: UserContextValue = {
  userId: DEFAULT_USER_ID,
};
