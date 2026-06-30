import { useEffect, useState } from "react";
import { getUserInteractions } from "../services/api";
import type { UserInteraction } from "../types";

type UseInteractionsState = {
  userId: string | null;
  data: UserInteraction[];
  loading: boolean;
  error: string | null;
};

export function useInteractions(userId: string, limit = 10) {
  const [refreshTick, setRefreshTick] = useState(0);
  const [state, setState] = useState<UseInteractionsState>({
    userId: null,
    data: [],
    loading: true,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    getUserInteractions(userId)
      .then((data) => {
        if (cancelled) return;
        setState({
          userId,
          data: data.slice(0, limit),
          loading: false,
          error: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          userId,
          data: [],
          loading: false,
          error: "Could not load recent activity.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId, limit, refreshTick]);

  useEffect(() => {
    const onInteractionUpdated = () => setRefreshTick((x) => x + 1);
    window.addEventListener("interaction-updated", onInteractionUpdated);
    return () => window.removeEventListener("interaction-updated", onInteractionUpdated);
  }, []);

  return {
    data: state.userId === userId ? state.data : [],
    loading: state.userId !== userId || state.loading,
    error: state.userId === userId ? state.error : null,
  };
}

