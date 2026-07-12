import type { AuthUser, Role } from "@/lib/types";

const TOKEN_KEY = "ojsharp.auth.token";
const USER_KEY = "ojsharp.auth.user";
const REMEMBER_KEY = "ojsharp.auth.remember";

function readStorage(): Storage | null {
  if (typeof window === "undefined") {
    return null;
  }

  clearLegacySharedAuth();
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

export function storeAuth(token: string, user: AuthUser) {
  if (typeof window === "undefined") {
    return;
  }

  clearStoredAuthSilent();
  const store = window.sessionStorage;
  store.setItem(TOKEN_KEY, token);
  store.setItem(USER_KEY, JSON.stringify(user));
  notifyAuthSubscribers();
}

export function stageAuthToken(token: string) {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  clearLegacySharedAuth();
  const previousToken = window.sessionStorage.getItem(TOKEN_KEY);
  const previousUser = window.sessionStorage.getItem(USER_KEY);
  window.sessionStorage.setItem(TOKEN_KEY, token);
  window.sessionStorage.removeItem(USER_KEY);

  return () => {
    restoreStorageValue(window.sessionStorage, TOKEN_KEY, previousToken);
    restoreStorageValue(window.sessionStorage, USER_KEY, previousUser);
  };
}

export function clearStoredAuth() {
  clearStoredAuthSilent();
  notifyAuthSubscribers();
}

function clearStoredAuthSilent() {
  if (typeof window === "undefined") {
    return;
  }

  window.sessionStorage.removeItem(TOKEN_KEY);
  window.sessionStorage.removeItem(USER_KEY);
  clearLegacySharedAuth();
}

function clearLegacySharedAuth() {
  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USER_KEY);
  window.localStorage.removeItem(REMEMBER_KEY);
}

function restoreStorageValue(store: Storage, key: string, value: string | null) {
  if (value === null) {
    store.removeItem(key);
  } else {
    store.setItem(key, value);
  }
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
