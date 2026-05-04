import type {
  Analytics,
  CreateInteractionRequest,
  Mastery,
  Recommendation,
  StudyTask,
  UserInteraction,
  UserProfile,
  UserXp,
} from "../types";
import {
  clearStoredAuthSession,
  getStoredAuthSession,
  setStoredAuthSession,
  type StoredAuthSession,
} from "./authStorage";

const API_BASE = import.meta.env.VITE_API_URL?.trim();
if (!API_BASE) {
  throw new Error("Missing VITE_API_URL. Configure frontend environment variables.");
}

type ApiErrorResponse = {
  status: number;
  error: string;
  timestamp: string;
};

type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  userId: string;
  email: string;
};

type CurrentUserResponse = {
  userId: string;
  email: string;
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

let refreshInFlight: Promise<boolean> | null = null;

function applyAuthHeader(init?: RequestInit): RequestInit {
  const session = getStoredAuthSession();
  if (!session) return init ?? {};

  const headers = new Headers(init?.headers ?? {});
  headers.set("Authorization", `Bearer ${session.accessToken}`);

  return { ...init, headers };
}

async function tryRefreshToken(): Promise<boolean> {
  const session = getStoredAuthSession();
  if (!session) return false;

  if (refreshInFlight) {
    return refreshInFlight;
  }

  refreshInFlight = (async () => {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    });

    if (!res.ok) {
      clearStoredAuthSession();
      return false;
    }

    const auth = (await res.json()) as AuthResponse;
    setStoredAuthSession({
      accessToken: auth.accessToken,
      refreshToken: auth.refreshToken,
      userId: auth.userId,
      email: auth.email,
    });
    return true;
  })();

  try {
    return await refreshInFlight;
  } finally {
    refreshInFlight = null;
  }
}

async function fetcher<T>(url: string, init?: RequestInit, retryOnUnauthorized = true): Promise<T> {
  const res = await fetch(url, applyAuthHeader(init));

  if (res.status === 401 && retryOnUnauthorized) {
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      return fetcher<T>(url, init, false);
    }
  }

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

export type GenerateRecommendationsResponse = {
  userId: string;
  generatedCount: number;
};

export const generateRecommendationsForUser = (userId: string): Promise<GenerateRecommendationsResponse> =>
  fetcher<GenerateRecommendationsResponse>(`${API_BASE}/users/${userId}/recommendations/generate`, {
    method: "POST",
  });


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

export const getUserTasks = (userId: string): Promise<StudyTask[]> =>
  fetcher<StudyTask[]>(`${API_BASE}/users/${userId}/tasks`);

export const updateUserTaskStatus = (
  userId: string,
  taskId: string,
  status: "Pending" | "Completed" | "Overdue",
): Promise<StudyTask> =>
  fetcher<StudyTask>(`${API_BASE}/users/${userId}/tasks/${taskId}/status`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ status }),
  });

export type CreateStudyTaskRequest = {
  learningResourceId?: string | null;
  title: string;
  notes?: string | null;
  deadlineUtc: string;
  estimatedMinutes: number;
  priority: number;
};

export type UpdateStudyTaskRequest = CreateStudyTaskRequest;

export const createUserTask = (userId: string, payload: CreateStudyTaskRequest): Promise<StudyTask> =>
  fetcher<StudyTask>(`${API_BASE}/users/${userId}/tasks`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

export const updateUserTask = (userId: string, taskId: string, payload: UpdateStudyTaskRequest): Promise<StudyTask> =>
  fetcher<StudyTask>(`${API_BASE}/users/${userId}/tasks/${taskId}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

export const deleteUserTask = (userId: string, taskId: string): Promise<void> =>
  fetcher<void>(`${API_BASE}/users/${userId}/tasks/${taskId}`, {
    method: "DELETE",
  });

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

function mapAuthToSession(auth: AuthResponse): StoredAuthSession {
  return {
    accessToken: auth.accessToken,
    refreshToken: auth.refreshToken,
    userId: auth.userId,
    email: auth.email,
  };
}

export const register = async (payload: { email: string; password: string; dailyAvailableMinutes: number }) => {
  const auth = await fetcher<AuthResponse>(
    `${API_BASE}/auth/register`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    },
    false,
  );
  const session = mapAuthToSession(auth);
  setStoredAuthSession(session);
  return session;
};

export const login = async (payload: { email: string; password: string }) => {
  const auth = await fetcher<AuthResponse>(
    `${API_BASE}/auth/login`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    },
    false,
  );
  const session = mapAuthToSession(auth);
  setStoredAuthSession(session);
  return session;
};

export const getCurrentUser = () => fetcher<CurrentUserResponse>(`${API_BASE}/auth/me`);

export const logout = async () => {
  const session = getStoredAuthSession();
  if (!session) return;

  await fetcher<void>(
    `${API_BASE}/auth/logout`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    },
    false,
  );
  clearStoredAuthSession();
};
