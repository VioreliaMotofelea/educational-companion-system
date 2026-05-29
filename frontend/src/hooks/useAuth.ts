import { useContext } from "react";
import { AuthContext } from "../context/AuthContextCore";

export function useAuth() {
  return useContext(AuthContext);
}
