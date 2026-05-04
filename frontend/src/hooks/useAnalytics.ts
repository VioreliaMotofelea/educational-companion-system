import { useEffect, useState } from "react";
import { getUserAnalytics } from "../services/api";
import type { Analytics } from "../types";

export function useAnalytics(userId: string) {
  const [data, setData] = useState<Analytics | null>(null);
  const [refreshTick, setRefreshTick] = useState(0);

  useEffect(() => {
    let cancelled = false;

    getUserAnalytics(userId)
      .then((analytics) => {
        if (!cancelled) setData(analytics);
      })
      .catch(() => {
        if (!cancelled) setData(null);
      });

    return () => {
      cancelled = true;
    };
  }, [userId, refreshTick]);

  useEffect(() => {
    const onInteractionUpdated = () => setRefreshTick((x) => x + 1);
    const onTaskUpdated = () => setRefreshTick((x) => x + 1);
    window.addEventListener("interaction-updated", onInteractionUpdated);
    window.addEventListener("task-updated", onTaskUpdated);
    return () => {
      window.removeEventListener("interaction-updated", onInteractionUpdated);
      window.removeEventListener("task-updated", onTaskUpdated);
    };
  }, []);

  return data;
}