import { useEffect, useState } from "react";
import { getUserProfile } from "../services/api";
import type { UserProfile } from "../types";

export function useUser(userId: string) {
  const [user, setUser] = useState<UserProfile | null>(null);

  useEffect(() => {
    getUserProfile(userId).then(setUser).catch(() => setUser(null));
  }, [userId]);

  return user;
}