import type { Analytics, Mastery, Recommendation, UserProfile, UserXp } from "../types";

const API_BASE = import.meta.env.VITE_API_URL;

type ApiErrorResponse = {
  status: number;
  error: string;
  timestamp: string;
};

export type InteractionType = "Viewed" | "Completed" | "Rated" | "Skipped";

export type CreateInteractionRequest = {
  userId: string;
  learningResourceId: string;
  interactionType: InteractionType;
  rating?: number;
  timeSpentMinutes?: number;
};

export type UserInteraction = {
  id: string;
  userId: string;
  learningResourceId: string;
  interactionType: InteractionType;
  rating: number | null;
  timeSpentMinutes: number | null;
  createdAtUtc: string;
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
  fetcher<unknown[]>(`${API_BASE}/resources`);


export const createInteraction = (payload: CreateInteractionRequest): Promise<UserInteraction> =>
  fetcher<UserInteraction>(`${API_BASE}/interactions`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
