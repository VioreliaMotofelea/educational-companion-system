import { useContext } from "react";
import { UserContext } from "../context/userContextValue";

export function useCurrentUser() {
  return useContext(UserContext);
}
