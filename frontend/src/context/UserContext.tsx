import { UserContext, defaultUserContextValue } from "./userContextValue";

type UserProviderProps = {
  children: React.ReactNode;
};

export function UserProvider({ children }: UserProviderProps) {
  return (
    <UserContext.Provider value={defaultUserContextValue}>
      {children}
    </UserContext.Provider>
  );
}
