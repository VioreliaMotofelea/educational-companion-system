import { useCallback, useEffect, useRef, useState } from "react";
import { generateRecommendationsForUser, getRecommendations } from "../services/api";
import type { Recommendation } from "../types";

type UseRecommendationsState = {
  userId: string | null;
  data: Recommendation[];
  loading: boolean;
  error: string | null;
};

export function useRecommendations(userId: string, limit = 5) {
  const [reloadKey, setReloadKey] = useState(0);
  const [state, setState] = useState<UseRecommendationsState>({
    userId: null,
    data: [],
    loading: true,
    error: null,
  });

  const triedGenerateForLoadRef = useRef(false);

  useEffect(() => {
    let cancelled = false;
    triedGenerateForLoadRef.current = false;

    queueMicrotask(() => {
      if (cancelled) return;

      if (!userId) {
        setState({ userId: null, data: [], loading: false, error: null });
        return;
      }

      setState((prev) => ({
        ...prev,
        userId,
        loading: true,
        error: null,
      }));

      const fail = (message: string) => {
        if (cancelled) return;
        setState({
          userId,
          data: [],
          loading: false,
          error: message,
        });
      };

      const succeed = (data: Recommendation[]) => {
        if (cancelled) return;
        setState({
          userId,
          data,
          loading: false,
          error: null,
        });
      };

      void (async () => {
        try {
          let data = await getRecommendations(userId, limit);
          if (cancelled) return;

          if (data.length === 0 && !triedGenerateForLoadRef.current) {
            triedGenerateForLoadRef.current = true;
            try {
              await generateRecommendationsForUser(userId);
            } catch (genErr) {
              fail(
                genErr instanceof Error
                  ? genErr.message
                  : "We could not refresh your picks right now. Check your connection and try again.",
              );
              return;
            }
            data = await getRecommendations(userId, limit);
            if (cancelled) return;
          }

          succeed(data);
        } catch (err) {
          if (cancelled) return;
          fail(
            err instanceof Error
              ? err.message
              : "Something went wrong while loading recommendations.",
          );
        }
      })();
    });

    return () => {
      cancelled = true;
    };
  }, [userId, limit, reloadKey]);

  const refetch = useCallback(() => {
    setReloadKey((k) => k + 1);
  }, []);

  return {
    data: state.userId === userId ? state.data : [],
    loading: state.userId !== userId || state.loading,
    error: state.userId === userId ? state.error : null,
    refetch,
  };
}
