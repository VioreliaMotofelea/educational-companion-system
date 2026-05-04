const ACCESS_TOKEN_KEY = "ecs.accessToken";
const REFRESH_TOKEN_KEY = "ecs.refreshToken";
const USER_ID_KEY = "ecs.userId";
const USER_EMAIL_KEY = "ecs.userEmail";

export type StoredAuthSession = {
  accessToken: string;
  refreshToken: string;
  userId: string;
  email: string;
};

function getItem(key: string) {
  return window.localStorage.getItem(key);
}

export function getStoredAuthSession(): StoredAuthSession | null {
  const accessToken = getItem(ACCESS_TOKEN_KEY);
  const refreshToken = getItem(REFRESH_TOKEN_KEY);
  const userId = getItem(USER_ID_KEY);
  const email = getItem(USER_EMAIL_KEY);

  if (!accessToken || !refreshToken || !userId || !email) {
    return null;
  }

  return { accessToken, refreshToken, userId, email };
}

export function setStoredAuthSession(session: StoredAuthSession) {
  window.localStorage.setItem(ACCESS_TOKEN_KEY, session.accessToken);
  window.localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
  window.localStorage.setItem(USER_ID_KEY, session.userId);
  window.localStorage.setItem(USER_EMAIL_KEY, session.email);
}

export function clearStoredAuthSession() {
  window.localStorage.removeItem(ACCESS_TOKEN_KEY);
  window.localStorage.removeItem(REFRESH_TOKEN_KEY);
  window.localStorage.removeItem(USER_ID_KEY);
  window.localStorage.removeItem(USER_EMAIL_KEY);
}
