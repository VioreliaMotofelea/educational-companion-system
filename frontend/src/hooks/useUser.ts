import { useEffect, useState } from "react";
import { getUserProfile } from "../services/api";
import type { UserProfile } from "../types";

export function useUser(userId: string) {
  const [refreshTick, setRefreshTick] = useState(0);
  const [state, setState] = useState<{
    loadedForUserId: string | null;
    user: UserProfile | null;
    loading: boolean;
    error: string | null;
  }>({
    loadedForUserId: null,
    user: null,
    loading: true,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    getUserProfile(userId)
      .then((profile) => {
        if (cancelled) return;
        setState({
          loadedForUserId: userId,
          user: profile,
          loading: false,
          error: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          loadedForUserId: userId,
          user: null,
          loading: false,
          error: "Could not load profile.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId, refreshTick]);

  const refresh = () => setRefreshTick((t) => t + 1);

  return {
    user: state.loadedForUserId === userId ? state.user : null,
    loading: state.loadedForUserId !== userId || state.loading,
    error: state.loadedForUserId === userId ? state.error : null,
    refresh,
  };
}