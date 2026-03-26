import type {
  Analytics,
  CreateInteractionRequest,
  Mastery,
  Recommendation,
  UserInteraction,
  UserProfile,
  UserXp,
} from "../types";

const API_BASE = import.meta.env.VITE_API_URL?.trim();
if (!API_BASE) {
  throw new Error("Missing VITE_API_URL. Configure frontend environment variables.");
}

type ApiErrorResponse = {
  status: number;
  error: string;
  timestamp: string;
};

export type LearningResource = {
  id: string;
  title: string;
  description: string | null;
  topic: string;
  difficulty: number;
  estimatedDurationMinutes: number;
  contentType: "Article" | "Video" | "Quiz";
};

async function fetcher<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);

  if (!res.ok) {
    let message = "API error";

    try {
      const parsed = (await res.json()) as Partial<ApiErrorResponse>;
      if (parsed.error) {
        message = parsed.error;
      }
    } catch {
      // no-op: keep generic message when server does not send JSON
    }

    throw new Error(message);
  }

  // Backend uses 204 for some update operations (e.g. preferences).
  if (res.status === 204) {
    return undefined as T;
  }

  return res.json();
}

export const getUserProfile = (userId: string): Promise<UserProfile> =>
  fetcher<UserProfile>(`${API_BASE}/users/${userId}`);

export const getUserAnalytics = (userId: string): Promise<Analytics> =>
  fetcher<Analytics>(`${API_BASE}/users/${userId}/analytics`);

export const getUserMastery = (userId: string): Promise<Mastery> =>
  fetcher<Mastery>(`${API_BASE}/users/${userId}/mastery`);

export const getUserXP = (userId: string): Promise<UserXp> =>
  fetcher<UserXp>(`${API_BASE}/users/${userId}/xp`);


export const getRecommendations = (userId: string, limit = 5): Promise<Recommendation[]> =>
  fetcher<Recommendation[]>(`${API_BASE}/users/${userId}/recommendations?limit=${limit}`);


export const getResources = () =>
  fetcher<LearningResource[]>(`${API_BASE}/resources`);


export const createInteraction = (payload: CreateInteractionRequest): Promise<UserInteraction> =>
  fetcher<UserInteraction>(`${API_BASE}/interactions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

export const getUserInteractions = (userId: string): Promise<UserInteraction[]> =>
  fetcher<UserInteraction[]>(`${API_BASE}/users/${userId}/interactions`);

export type UpdateUserPreferencesRequest = {
  preferredDifficulty?: number | null;
  preferredContentTypesCsv?: string | null;
  preferredTopicsCsv?: string | null;
};

export const updateUserPreferences = (
  userId: string,
  payload: UpdateUserPreferencesRequest,
): Promise<void> =>
  fetcher<void>(`${API_BASE}/users/${userId}/preferences`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
