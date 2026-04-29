import { createContext } from "react";

export type UserContextValue = {
  userId: string;
};

export const UserContext = createContext<UserContextValue>({
  userId: "",
});

export const defaultUserContextValue: UserContextValue = {
  userId: "",
};
