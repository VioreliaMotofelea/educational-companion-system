import { useEffect, useMemo, useState } from "react";
import { getAccessibleResources, type AccessibleLearningResource } from "../services/api";

export function useAccessibleResources(userId: string) {
  const [state, setState] = useState<{
    userId: string | null;
    data: AccessibleLearningResource[];
    loading: boolean;
    error: string | null;
  }>({
    userId: null,
    data: [],
    loading: true,
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    if (!userId) {
      setState({ userId: null, data: [], loading: false, error: null });
      return;
    }

    setState((prev) => ({
      userId,
      data: prev.userId === userId ? prev.data : [],
      loading: prev.userId !== userId || prev.data.length === 0,
      error: null,
    }));

    void getAccessibleResources(userId)
      .then((data) => {
        if (cancelled) return;
        setState({ userId, data, loading: false, error: null });
      })
      .catch(() => {
        if (cancelled) return;
        setState({ userId, data: [], loading: false, error: "Could not load accessible resources." });
      });

    return () => {
      cancelled = true;
    };
  }, [userId]);

  const byId = useMemo(() => {
    const map = new Map<string, AccessibleLearningResource>();
    for (const resource of state.userId === userId ? state.data : []) {
      map.set(resource.id, resource);
    }
    return map;
  }, [state.data, state.userId, userId]);

  return {
    resources: state.userId === userId ? state.data : [],
    resourceById: byId,
    loading: state.userId !== userId || state.loading,
    error: state.userId === userId ? state.error : null,
  };
}
