"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Code2 } from "lucide-react";
import { consumeGoogleCallback } from "@/lib/api";

export default function GoogleCallbackPage() {
  return (
    <Suspense fallback={<GoogleCallbackShell status="Finishing sign-in..." />}>
      <GoogleCallbackContent />
    </Suspense>
  );
}

function GoogleCallbackContent() {
  const router = useRouter();
  const params = useSearchParams();
  const token = params.get("token");
  const errorParam = params.get("error");
  const initialError = errorParam
    ? describeError(errorParam)
    : token
      ? null
      : "Google did not return a token. Please try signing in again.";
  const [asyncError, setAsyncError] = useState<string | null>(null);
  const error = asyncError ?? initialError;
  const status = error ? "" : "Finishing sign-in...";

  useEffect(() => {
    if (initialError || !token) {
      return;
    }

    const rememberFromUrl = params.get("remember_me") === "1";
    const rememberFromSession =
      typeof window !== "undefined"
        && window.sessionStorage.getItem("ojsharp.auth.googleRememberMe") === "1";
    const rememberMe = rememberFromUrl || rememberFromSession;

    consumeGoogleCallback(token, rememberMe)
      .then((user) => {
        if (typeof window !== "undefined") {
          window.sessionStorage.removeItem("ojsharp.auth.googleRememberMe");
        }
        router.replace(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
      })
      .catch((exception: unknown) => {
        setAsyncError(exception instanceof Error ? exception.message : "Could not complete Google sign-in.");
      });
  }, [initialError, params, router, token]);

  return <GoogleCallbackShell status={status} error={error} onBackToLogin={() => router.replace("/login")} />;
}

function GoogleCallbackShell({ status, error, onBackToLogin }: { status: string; error?: string | null; onBackToLogin?: () => void }) {
  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon mx-auto w-full max-w-md rounded-3xl p-10 text-center">
        <span className="inline-grid h-12 w-12 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow">
          <Code2 size={22} />
        </span>
        <h1 className="mt-6 font-heading text-3xl italic text-white">Signing you in</h1>
        {status ? (
          <p className="mt-4 text-sm text-white/60">{status}</p>
        ) : null}
        {error ? (
          <>
            <p className="mt-4 text-sm text-pinkGlow">{error}</p>
            <button
              type="button"
              onClick={onBackToLogin}
              className="btn-secondary mt-6"
            >
              Back to sign in
            </button>
          </>
        ) : null}
      </section>
    </main>
  );
}

function describeError(code: string) {
  switch (code) {
    case "invalid_state":
      return "Your sign-in session expired. Please start again.";
    case "missing_code_or_state":
      return "Google did not return the expected response.";
    case "google_exchange_failed":
      return "We could not verify your Google account. Please try again.";
    case "account_inactive":
      return "This account is inactive. Contact an administrator.";
    case "access_denied":
      return "You cancelled the Google sign-in.";
    default:
      return `Google sign-in failed (${code}).`;
  }
}
