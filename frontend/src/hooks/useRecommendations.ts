import { useEffect, useRef, useState } from "react";
import { generateRecommendationsForUser, getRecommendations } from "../services/api";
import type { Recommendation } from "../types";

type UseRecommendationsState = {
  userId: string | null;
  data: Recommendation[];
  loading: boolean;
};

export function useRecommendations(userId: string, limit = 5) {
  const [state, setState] = useState<UseRecommendationsState>({
    userId: null,
    data: [],
    loading: true,
  });

  const generatedOnceRef = useRef(false);

  useEffect(() => {
    let cancelled = false;

    // Reset generation guard when user changes.
    generatedOnceRef.current = false;

    if (!userId) {
      setState({ userId: userId ?? null, data: [], loading: false });
      return;
    }

    getRecommendations(userId, limit)
      .then((data) => {
        if (cancelled) return;

        if (data.length === 0 && !generatedOnceRef.current) {
          generatedOnceRef.current = true;

          return generateRecommendationsForUser(userId)
            .then(() => getRecommendations(userId, limit))
            .then((retryData: Recommendation[]) => {
              if (cancelled) return;
              setState({
                userId,
                data: retryData,
                loading: false,
              });
            })
            .catch(() => {
              if (cancelled) return;
              // Keep empty state if generation fails.
              setState({
                userId,
                data: [],
                loading: false,
              });
            });
        }

        setState({ userId, data, loading: false });
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
  }, [userId, limit]);

  return {
    data: state.userId === userId ? state.data : [],
    loading: state.userId !== userId || state.loading,
  };
}