"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Code2, KeyRound, LogIn, UserPlus } from "lucide-react";
import {
  getRememberMePreference,
  getStoredUser,
  login,
  startGoogleLogin
} from "@/lib/api";

type AuthAction = "login" | "google";
const localDemoAdmin = {
  email: "admin@example.com",
  password: "Admin123!"
};
const showLocalDemoAccount = process.env.NODE_ENV !== "production";

export default function LoginPage() {
  return (
    <Suspense fallback={<LoginShell />}>
      <LoginContent />
    </Suspense>
  );
}

function LoginContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const prefillEmail = searchParams.get("email") ?? "";
  const [username, setUsername] = useState(prefillEmail);
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(() => getRememberMePreference());
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(
    prefillEmail ? `You already have an account with ${prefillEmail}. Sign in to continue.` : null
  );
  const [submittingAction, setSubmittingAction] = useState<AuthAction | null>(null);

  useEffect(() => {
    const user = getStoredUser();
    if (user) {
      router.replace(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
    }
  }, [router]);

  async function handleSignIn(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setSubmittingAction("login");
    try {
      const { user, mustChangePassword } = await login(username, password, rememberMe);
      if (mustChangePassword) {
        router.push("/change-password");
        return;
      }
      router.push(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Authentication failed.");
    } finally {
      setSubmittingAction(null);
    }
  }

  async function handleGoogleSignIn() {
    setError(null);
    setNotice(null);
    setSubmittingAction("google");
    try {
      await startGoogleLogin(rememberMe);
      // Browser will navigate away; nothing else to do.
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Google sign-in is unavailable.");
      setSubmittingAction(null);
    }
  }

  function fillLocalAdminAccount() {
    setUsername(localDemoAdmin.email);
    setPassword(localDemoAdmin.password);
    setError(null);
    setNotice("Local administrator account filled.");
  }

  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon grid w-full max-w-5xl overflow-hidden rounded-3xl lg:grid-cols-[1.1fr_0.9fr]">
        <div className="relative p-8 lg:p-12">
          <div className="mb-12 inline-flex items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
            <span className="grid h-9 w-9 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow">
              <Code2 size={19} />
            </span>
            <span>
              <span className="block text-sm font-semibold">AI Coding Assessment</span>
              <span className="block text-xs text-white/45">Secure assessment workspace</span>
            </span>
          </div>
          <h1 className="font-heading text-5xl italic leading-tight text-white lg:text-7xl">Enter the assessment workspace.</h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-white/60">
            Sign in with your account, continue with Google, or register a new student account. Administrator accounts are created by an administrator.
          </p>

          <button
            type="button"
            onClick={handleGoogleSignIn}
            disabled={submittingAction !== null}
            className="mt-8 flex w-full max-w-xl items-center justify-center gap-3 rounded-xl border border-white/10 bg-white px-4 py-3 text-sm font-semibold text-gray-800 transition hover:bg-white/90 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <GoogleIcon />
            {submittingAction === "google" ? "Redirecting to Google..." : "Continue with Google"}
          </button>

          <div className="mt-6 flex max-w-xl items-center gap-3 text-xs uppercase tracking-wider text-white/35">
            <span className="h-px flex-1 bg-white/10" />
            or with username
            <span className="h-px flex-1 bg-white/10" />
          </div>

          <form className="mt-6 grid max-w-xl gap-4" onSubmit={handleSignIn}>
            <label className="grid gap-2 text-sm text-white/60">
              Username
              <input className="field" type="text" value={username} onChange={(event) => setUsername(event.target.value)} required autoComplete="username" />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Password
              <input className="field" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={6} autoComplete="current-password" />
            </label>
            <div className="mt-1 flex items-center justify-between">
              <label className="flex cursor-pointer items-center gap-2 text-sm text-white/70 select-none">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(event) => setRememberMe(event.target.checked)}
                  className="h-4 w-4 rounded border-white/20 bg-white/5 text-cyanGlow accent-cyanGlow"
                />
                Remember me on this device
              </label>
              <Link
                href={username.includes("@") ? `/forgot-password?email=${encodeURIComponent(username)}` : "/forgot-password"}
                className="text-sm text-cyanGlow hover:underline"
              >
                Forgot password?
              </Link>
            </div>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submittingAction !== null}>
                <LogIn size={18} />
                {submittingAction === "login" ? "Connecting..." : "Sign in"}
              </button>
              {showLocalDemoAccount ? (
                <button className="btn-secondary" type="button" onClick={fillLocalAdminAccount} disabled={submittingAction !== null}>
                  <KeyRound size={18} />
                  Use local admin
                </button>
              ) : null}
              <Link className="btn-secondary" href="/register">
                <UserPlus size={18} />
                Register Student
              </Link>
            </div>
          </form>
          {notice ? <p className="mt-4 text-sm text-cyanGlow">{notice}</p> : null}
          {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
        </div>
        <div className="border-t border-white/10 bg-black/20 p-8 lg:border-l lg:border-t-0 lg:p-12">
          <div className="rounded-3xl border border-white/10 bg-black/30 p-6">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-cyanGlow">Built for focused assessment</p>
            <h2 className="mt-4 text-2xl font-semibold text-white">Everything you need, without leaving the workspace.</h2>
            <ul className="mt-5 grid gap-3 text-sm leading-6 text-white/60">
              <li>Work across realistic project files in one secure editor.</li>
              <li>Run visible checks and preview interface tasks as you build.</li>
              <li>Keep progress saved while the assessment is active.</li>
            </ul>
          </div>
          <div className="mt-6 grid gap-3 text-sm text-white/55">
            {showLocalDemoAccount ? (
              <p className="rounded-2xl border border-white/10 bg-white/5 p-4">
                Local admin: <span className="text-white">admin@example.com</span> / <span className="text-white">Admin123!</span>
              </p>
            ) : null}
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Email signup requires verification before the first sign-in.</p>
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Google sign-in skips verification because Google has already confirmed the address.</p>
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">&quot;Remember me&quot; keeps you signed in on this device for 30 days. Unchecked, the session ends when you close the browser.</p>
          </div>
        </div>
      </section>
    </main>
  );
}

function GoogleIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
      <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.76h3.56c2.08-1.92 3.28-4.74 3.28-8.09Z" />
      <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.56-2.76c-.99.66-2.25 1.06-3.72 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84A11 11 0 0 0 12 23Z" />
      <path fill="#FBBC05" d="M5.84 14.11A6.6 6.6 0 0 1 5.5 12c0-.73.12-1.44.34-2.11V7.05H2.18a11 11 0 0 0 0 9.9l3.66-2.84Z" />
      <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.65l3.15-3.15A11 11 0 0 0 12 1 11 11 0 0 0 2.18 7.05l3.66 2.84C6.71 7.31 9.14 5.38 12 5.38Z" />
    </svg>
  );
}

function LoginShell() {
  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon w-full max-w-md rounded-3xl p-10">
        <span className="inline-grid h-12 w-12 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow">
          <Code2 size={22} />
        </span>
        <h1 className="mt-6 font-heading text-3xl italic text-white">Enter the assessment workspace.</h1>
        <p className="mt-3 text-sm text-white/60">Loading sign-in...</p>
      </section>
    </main>
  );
}
