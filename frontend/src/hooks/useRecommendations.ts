import { useEffect, useState } from "react";
import { getRecommendations } from "../services/api";
import type { Recommendation } from "../types";

export function useRecommendations(userId: string) {
  const [data, setData] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    getRecommendations(userId)
      .then(setData)
      .catch(() => setData([]))
      .finally(() => setLoading(false));
  }, [userId]);

  return { data, loading };
}