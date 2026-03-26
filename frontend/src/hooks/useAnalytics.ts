import { useEffect, useState } from "react";
import { getUserAnalytics } from "../services/api";
import type { Analytics } from "../types";

export function useAnalytics(userId: string) {
  const [data, setData] = useState<Analytics | null>(null);

  useEffect(() => {
    getUserAnalytics(userId).then(setData).catch(() => setData(null));
  }, [userId]);

  return data;
}