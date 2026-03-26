import { useEffect, useState } from "react";
import { getUserProfile } from "../services/api";
import type { UserProfile } from "../types";

export function useUser(userId: string) {
  const [user, setUser] = useState<UserProfile | null>(null);

  useEffect(() => {
    let cancelled = false;

    getUserProfile(userId)
      .then((profile) => {
        if (!cancelled) setUser(profile);
      })
      .catch(() => {
        if (!cancelled) setUser(null);
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  return user;
}