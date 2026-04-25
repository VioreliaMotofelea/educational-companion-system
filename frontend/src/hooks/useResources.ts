import { useEffect, useState } from "react";
import { getResources } from "../services/api";
import type { LearningResource } from "../services/api";

type UseResourcesState = {
  loaded: boolean;
  loading: boolean;
  data: LearningResource[];
  error: string | null;
};

export function useResources() {
  const [state, setState] = useState<UseResourcesState>({
    loaded: false,
    loading: true,
    data: [],
    error: null,
  });

  useEffect(() => {
    let cancelled = false;

    getResources()
      .then((data) => {
        if (cancelled) return;
        setState({
          loaded: true,
          loading: false,
          data,
          error: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          loaded: true,
          loading: false,
          data: [],
          error: "Could not load resources catalog.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}

