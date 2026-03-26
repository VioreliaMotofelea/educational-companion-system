import { useEffect, useState } from "react";
import { getRecommendations } from "../services/api";
import type { Recommendation } from "../types";

type UseRecommendationsState = {
  userId: string | null;
  data: Recommendation[];
  loading: boolean;
};

export function useRecommendations(userId: string) {
  const [state, setState] = useState<UseRecommendationsState>({
    userId: null,
    data: [],
    loading: true,
  });

  useEffect(() => {
    let cancelled = false;

    getRecommendations(userId)
      .then((data) => {
        if (cancelled) return;
        setState({
          userId,
          data,
          loading: false,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          userId,
          data: [],
          loading: false,
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  return {
    data: state.userId === userId ? state.data : [],
    loading: state.userId !== userId || state.loading,
  };
}