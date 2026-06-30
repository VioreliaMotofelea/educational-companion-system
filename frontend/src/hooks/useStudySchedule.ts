import { useCallback, useEffect, useState } from "react";
import { getTodayStudySchedule } from "../services/api";
import type { StudyDaySchedule } from "../types";
import { INTERACTION_UPDATED_EVENT } from "./useRecommendations";

type State = {
  userId: string | null;
  data: StudyDaySchedule | null;
  loading: boolean;
  error: string | null;
};

export function useStudySchedule(userId: string) {
  const [refreshTick, setRefreshTick] = useState(0);
  const [state, setState] = useState<State>({
    userId: null,
    data: null,
    loading: true,
    error: null,
  });

  const refresh = useCallback(() => {
    setRefreshTick((tick) => tick + 1);
  }, []);

  useEffect(() => {
    let cancelled = false;

    if (!userId) {
      setState({ userId: null, data: null, loading: false, error: null });
      return;
    }

    setState((prev) => ({
      ...prev,
      userId,
      loading: prev.data === null,
      error: null,
    }));

    void getTodayStudySchedule(userId)
      .then((data) => {
        if (cancelled) return;
        setState({ userId, data, loading: false, error: null });
      })
      .catch((err) => {
        if (cancelled) return;
        setState({
          userId,
          data: null,
          loading: false,
          error: err instanceof Error ? err.message : "Could not load today's study plan.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [userId, refreshTick]);

  useEffect(() => {
    const onRefresh = () => refresh();
    window.addEventListener(INTERACTION_UPDATED_EVENT, onRefresh);
    window.addEventListener("task-updated", onRefresh);
    return () => {
      window.removeEventListener(INTERACTION_UPDATED_EVENT, onRefresh);
      window.removeEventListener("task-updated", onRefresh);
    };
  }, [refresh]);

  return {
    schedule: state.userId === userId ? state.data : null,
    loading: state.userId !== userId || state.loading,
    error: state.userId === userId ? state.error : null,
    refresh,
  };
}
