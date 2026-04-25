import { useEffect, useState } from "react";
import { getUserAnalytics } from "../services/api";
import type { Analytics } from "../types";

export function useAnalytics(userId: string) {
  const [data, setData] = useState<Analytics | null>(null);

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
  }, [userId]);

  return data;
}