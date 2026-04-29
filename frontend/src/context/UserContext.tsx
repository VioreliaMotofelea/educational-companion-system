import { UserContext, defaultUserContextValue } from "./userContextValue";
import { useAuth } from "../hooks/useAuth";

type UserProviderProps = {
  children: React.ReactNode;
};

export function UserProvider({ children }: UserProviderProps) {
  const { user } = useAuth();

  return (
    <UserContext.Provider value={user ? { userId: user.userId } : defaultUserContextValue}>
      {children}
    </UserContext.Provider>
  );
}
