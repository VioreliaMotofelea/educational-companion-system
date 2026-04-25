import { useEffect, useState } from "react";
import { getUserMastery } from "../services/api";
import type { Mastery } from "../types";

export function useMastery(userId: string) {
  const [state, setState] = useState<{
    loadedForUserId: string | null;
    data: Mastery | null;
    loading: boolean;
    error: string | null;
  }>({
    loadedForUserId: null,
    data: null,
    loading: true,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    getUserMastery(userId)
      .then((m) => {
        if (cancelled) return;
        setState({
          loadedForUserId: userId,
          data: m,
          loading: false,
          error: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          loadedForUserId: userId,
          data: null,
          loading: false,
          error: "Could not load mastery.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  return {
    data: state.loadedForUserId === userId ? state.data : null,
    loading: state.loadedForUserId !== userId || state.loading,
    error: state.loadedForUserId === userId ? state.error : null,
  };
}

