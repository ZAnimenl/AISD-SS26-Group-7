import type { AuthUser, Role } from "@/lib/types";

const TOKEN_KEY = "ojsharp.auth.token";
const USER_KEY = "ojsharp.auth.user";

export function getStoredToken() {
  if (typeof window === "undefined") {
    return null;
  }

  return window.localStorage.getItem(TOKEN_KEY);
}

export function getStoredUser() {
  if (typeof window === "undefined") {
    return null;
  }

  const value = window.localStorage.getItem(USER_KEY);
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
  window.localStorage.setItem(TOKEN_KEY, token);
  window.localStorage.setItem(USER_KEY, JSON.stringify(user));
  notifyAuthSubscribers();
}

export function clearStoredAuth() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(TOKEN_KEY);
  window.localStorage.removeItem(USER_KEY);
  notifyAuthSubscribers();
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
