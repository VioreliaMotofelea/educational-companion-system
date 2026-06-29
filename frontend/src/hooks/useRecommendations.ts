import { useCallback, useEffect, useRef, useState } from "react";
import { generateRecommendationsForUser, getRecommendations } from "../services/api";
import type { Recommendation } from "../types";

export const INTERACTION_UPDATED_EVENT = "interaction-updated";

type UseRecommendationsState = {
  userId: string | null;
  data: Recommendation[];
  loading: boolean;
  error: string | null;
};

async function fetchRecommendations(userId: string, limit: number) {
  return getRecommendations(userId, limit);
}

async function generateAndFetch(userId: string, limit: number) {
  await generateRecommendationsForUser(userId);
  return fetchRecommendations(userId, limit);
}

export function useRecommendations(userId: string, limit = 5) {
  const [regenerating, setRegenerating] = useState(false);
  const [state, setState] = useState<UseRecommendationsState>({
    userId: null,
    data: [],
    loading: true,
    error: null,
  });

  const autoGenerateOnEmptyRef = useRef(true);
  const userIdRef = useRef(userId);
  userIdRef.current = userId;

  const loadRecommendations = useCallback(
    async (options: { showLoading: boolean; autoGenerateIfEmpty: boolean }) => {
      const activeUserId = userIdRef.current;
      if (!activeUserId) {
        setState({ userId: null, data: [], loading: false, error: null });
        return;
      }

      if (options.showLoading) {
        setState((prev) => ({
          ...prev,
          userId: activeUserId,
          loading: true,
          error: null,
        }));
      }

      try {
        let data = await fetchRecommendations(activeUserId, limit);

        if (data.length === 0 && options.autoGenerateIfEmpty && autoGenerateOnEmptyRef.current) {
          autoGenerateOnEmptyRef.current = false;
          data = await generateAndFetch(activeUserId, limit);
        }

        if (userIdRef.current !== activeUserId) return;

        setState({
          userId: activeUserId,
          data,
          loading: false,
          error: null,
        });
      } catch (err) {
        if (userIdRef.current !== activeUserId) return;

        const message =
          err instanceof Error
            ? err.message
            : "Something went wrong while loading recommendations.";

        setState((prev) => ({
          userId: activeUserId,
          data: options.showLoading ? [] : prev.data,
          loading: false,
          error: message,
        }));
      }
    },
    [limit],
  );

  useEffect(() => {
    autoGenerateOnEmptyRef.current = true;

    if (!userId) {
      setState({ userId: null, data: [], loading: false, error: null });
      return;
    }

    void loadRecommendations({ showLoading: true, autoGenerateIfEmpty: true });
  }, [userId, limit, loadRecommendations]);

  const silentRefresh = useCallback(async () => {
    await loadRecommendations({ showLoading: false, autoGenerateIfEmpty: true });
  }, [loadRecommendations]);

  useEffect(() => {
    if (!userId) return;

    const onInteractionUpdated = () => {
      void silentRefresh();
    };

    window.addEventListener(INTERACTION_UPDATED_EVENT, onInteractionUpdated);
    return () => {
      window.removeEventListener(INTERACTION_UPDATED_EVENT, onInteractionUpdated);
    };
  }, [userId, silentRefresh]);

  const refetch = useCallback(() => {
    void silentRefresh();
  }, [silentRefresh]);

  const regenerate = useCallback(async () => {
    if (!userId) return;

    setRegenerating(true);
    setState((prev) => ({
      ...prev,
      userId,
      error: null,
    }));

    try {
      const data = await generateAndFetch(userId, limit);
      setState({
        userId,
        data,
        loading: false,
        error: null,
      });
    } catch (err) {
      setState((prev) => ({
        ...prev,
        userId,
        loading: false,
        error:
          err instanceof Error
            ? err.message
            : "We could not regenerate your recommendations. Check your connection and try again.",
      }));
    } finally {
      setRegenerating(false);
    }
  }, [userId, limit]);

  const isInitialLoading = state.userId === userId && state.loading && state.data.length === 0;

  return {
    data: state.userId === userId ? state.data : [],
    loading: isInitialLoading,
    error: state.userId === userId ? state.error : null,
    refetch,
    regenerate,
    regenerating,
  };
}
