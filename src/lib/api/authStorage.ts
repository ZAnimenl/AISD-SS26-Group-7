import type { AuthUser, Role } from "@/lib/types";

const TOKEN_KEY = "ojsharp.auth.token";
const USER_KEY = "ojsharp.auth.user";
const REMEMBER_KEY = "ojsharp.auth.remember";

function readStorage(): Storage | null {
  if (typeof window === "undefined") {
    return null;
  }
  // Prefer the storage that currently holds the token.
  if (window.localStorage.getItem(TOKEN_KEY)) {
    return window.localStorage;
  }
  if (window.sessionStorage.getItem(TOKEN_KEY)) {
    return window.sessionStorage;
  }
  return null;
}

export function getStoredToken() {
  const store = readStorage();
  return store ? store.getItem(TOKEN_KEY) : null;
}

export function getStoredUser() {
  const store = readStorage();
  if (!store) {
    return null;
  }

  const value = store.getItem(USER_KEY);
  if (!value) {
    return null;
  }

  try {
    const user = JSON.parse(value) as Partial<AuthUser>;

    if (!isStoredAuthUser(user)) {
      clearStoredAuth();
      return null;
    }

    return user;
  } catch {
    clearStoredAuth();
    return null;
  }
}

export function storeAuth(token: string, user: AuthUser, options?: { rememberMe?: boolean }) {
  if (typeof window === "undefined") {
    return;
  }
  const rememberMe = options?.rememberMe ?? true;
  // Always clean both before writing to avoid stale entries.
  clearStoredAuthSilent();
  const store = rememberMe ? window.localStorage : window.sessionStorage;
  store.setItem(TOKEN_KEY, token);
  store.setItem(USER_KEY, JSON.stringify(user));
  // The remember flag itself goes to localStorage so we can prefill the checkbox later.
  if (rememberMe) {
    window.localStorage.setItem(REMEMBER_KEY, "1");
  } else {
    window.localStorage.removeItem(REMEMBER_KEY);
  }
  notifyAuthSubscribers();
}

export function clearStoredAuth() {
  clearStoredAuthSilent();
  notifyAuthSubscribers();
}

function clearStoredAuthSilent() {
  if (typeof window === "undefined") {
    return;
  }
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USER_KEY);
  window.sessionStorage.removeItem(TOKEN_KEY);
  window.sessionStorage.removeItem(USER_KEY);
}

export function getRememberMePreference() {
  if (typeof window === "undefined") {
    return true;
  }
  // Default to true so users get the friendlier behavior on first visit.
  return window.localStorage.getItem(REMEMBER_KEY) !== "0";
}

export function hasStoredAuth(expectedRole?: Role) {
  const token = getStoredToken();
  const user = getStoredUser();

  if (!token || !user) {
    return false;
  }

  return expectedRole ? user.role === expectedRole : true;
}

function isStoredAuthUser(user: Partial<AuthUser>): user is AuthUser {
  return typeof user.user_id === "string"
    && typeof user.email === "string"
    && (user.role === "student" || user.role === "administrator");
}

function notifyAuthSubscribers() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("ojsharp-auth"));
  }
}
