"use client";

import { useSyncExternalStore } from "react";
import { getStoredUser, hasStoredAuth } from "@/lib/api";

type AuthRole = "student" | "administrator";
type ClientAuthState = "checking" | "unauthenticated" | "wrong-role" | "authorized";

function subscribeToAuthStorage(onStoreChange: () => void) {
  if (typeof window === "undefined") {
    return () => {};
  }

  window.addEventListener("storage", onStoreChange);
  return () => window.removeEventListener("storage", onStoreChange);
}

function getServerSnapshot(): ClientAuthState {
  return "checking";
}

function getClientSnapshot(expectedRole: AuthRole): ClientAuthState {
  if (!hasStoredAuth()) {
    return "unauthenticated";
  }

  return getStoredUser()?.role === expectedRole ? "authorized" : "wrong-role";
}

export function useClientAuthState(expectedRole: AuthRole): ClientAuthState {
  return useSyncExternalStore(
    subscribeToAuthStorage,
    () => getClientSnapshot(expectedRole),
    getServerSnapshot
  );
}
