"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { BadgeCheck, Bot, CheckCircle2, Clock3, Code2, KeyRound, LogIn, MailCheck, PlayCircle, ShieldCheck, Sparkles, UserPlus } from "lucide-react";
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
const assessmentHighlights = [
  {
    icon: ShieldCheck,
    title: "Secure project space",
    copy: "Code, files, checks, and previews stay inside one assessment workspace."
  },
  {
    icon: CheckCircle2,
    title: "Visible progress",
    copy: "Run checks as you build and keep your saved work connected to the active session."
  },
  {
    icon: Sparkles,
    title: "Focused by design",
    copy: "A calm interface keeps attention on the task instead of setup friction."
  }
];
const signInNotes = [
  {
    icon: MailCheck,
    copy: "Email signup requires verification before the first sign-in."
  },
  {
    icon: BadgeCheck,
    copy: "Google sign-in uses the address already verified by Google."
  },
  {
    icon: Clock3,
    copy: '"Remember me" keeps this device signed in for 30 days.'
  }
];
const aiWorkflow = [
  { icon: LogIn, title: "Sign in", detail: "Verify" },
  { icon: Bot, title: "Workspace", detail: "Enter" },
  { icon: Code2, title: "Solve tasks", detail: "Code" },
  { icon: PlayCircle, title: "Run checks", detail: "Test" },
  { icon: CheckCircle2, title: "Submit", detail: "Score" }
];

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
      <section className="liquid-glass-neon grid w-full max-w-6xl overflow-hidden rounded-3xl lg:grid-cols-[1.12fr_0.88fr]">
        <div className="relative flex min-h-[680px] flex-col gap-7 p-6 lg:p-8">
          <div>
            <div className="mb-6 inline-flex items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 shadow-[0_18px_50px_rgba(0,0,0,0.18)]">
              <span className="grid h-9 w-9 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow">
                <Code2 size={19} />
              </span>
              <span>
                <span className="block text-sm font-semibold">AI Coding Assessment</span>
                <span className="block text-xs text-white/45">Secure assessment workspace</span>
              </span>
            </div>
            <h1 className="font-heading text-4xl italic leading-[0.98] text-white sm:text-5xl lg:text-5xl">Enter the AI assessment workspace.</h1>
            <div className="mt-5 max-w-xl rounded-2xl border border-white/10 bg-white/[0.045] p-4 shadow-[0_18px_50px_rgba(0,0,0,0.18)]">
              <div className="flex items-center gap-3">
                <span className="grid h-9 w-9 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow">
                  <Bot size={18} />
                </span>
                <span>
                  <span className="block text-sm font-semibold text-white">AI-guided assessment flow</span>
                  <span className="block text-sm text-white/50">A structured path from authentication to final scoring.</span>
                </span>
              </div>
              <div className="mt-5 flex items-start">
                  {aiWorkflow.map(({ icon: Icon, title, detail }, index) => (
                    <div key={title} className="contents">
                      <div className="grid min-h-[7.25rem] min-w-0 flex-1 justify-items-center rounded-2xl border border-white/10 bg-slate-950/70 px-2 py-3 text-center shadow-[inset_0_1px_0_rgba(255,255,255,0.05)]">
                        <span className="grid h-11 w-11 place-items-center rounded-2xl border border-cyanGlow/25 bg-cyanGlow/10 text-cyanGlow shadow-[0_0_24px_rgba(0,229,255,0.16)]">
                          <Icon size={17} />
                        </span>
                        <span className="mt-2 font-mono text-[10px] uppercase tracking-[0.14em] text-white/35">0{index + 1} {detail}</span>
                        <span className="mt-1 max-w-16 text-xs font-semibold leading-4 text-white/75">{title}</span>
                      </div>
                      {index < aiWorkflow.length - 1 ? (
                        <span className="mt-[3.55rem] h-0.5 w-5 shrink-0 rounded-full bg-gradient-to-r from-cyanGlow/75 via-cyanGlow/45 to-cyanGlow/75 shadow-[0_0_14px_rgba(0,229,255,0.38)]" />
                      ) : null}
                    </div>
                  ))}
              </div>
            </div>

          </div>

          <div className="max-w-xl rounded-3xl border border-white/10 bg-black/20 p-4 shadow-[0_24px_80px_rgba(0,0,0,0.2)]">
            <form className="grid gap-2.5" onSubmit={handleSignIn}>
              <label className="grid gap-2 text-sm text-white/60">
                Username
                <input className="field py-2" type="text" value={username} onChange={(event) => setUsername(event.target.value)} required autoComplete="username" />
              </label>
              <label className="grid gap-2 text-sm text-white/60">
                Password
                <input className="field py-2" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={6} autoComplete="current-password" />
              </label>
              <div className="mt-1 flex items-center gap-3 text-xs uppercase tracking-wider text-white/35">
                <span className="h-px flex-1 bg-white/10" />
                or
                <span className="h-px flex-1 bg-white/10" />
              </div>
              <button
                type="button"
                onClick={handleGoogleSignIn}
                disabled={submittingAction !== null}
                className="mt-1 flex w-full items-center justify-center gap-3 rounded-xl border border-white/10 bg-white px-4 py-2.5 text-sm font-semibold text-gray-800 shadow-[0_18px_40px_rgba(0,0,0,0.22)] transition hover:bg-white/90 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <GoogleIcon />
                {submittingAction === "google" ? "Redirecting to Google..." : "Continue with Google"}
              </button>
              <div className="mt-1 flex items-center justify-between gap-4">
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
                  className="shrink-0 text-sm text-cyanGlow hover:underline"
                >
                  Forgot password?
                </Link>
              </div>
              <div className="flex flex-wrap gap-2 pt-2">
                <button className="btn-primary py-2" type="submit" disabled={submittingAction !== null}>
                  <LogIn size={18} />
                  {submittingAction === "login" ? "Connecting..." : "Sign in"}
                </button>
                {showLocalDemoAccount ? (
                  <button className="btn-secondary py-2" type="button" onClick={fillLocalAdminAccount} disabled={submittingAction !== null}>
                    <KeyRound size={18} />
                    Use local admin
                  </button>
                ) : null}
                <Link className="btn-secondary py-2" href="/register">
                  <UserPlus size={18} />
                  Register Student
                </Link>
              </div>
            </form>
            {notice ? <p className="mt-3 text-sm text-cyanGlow">{notice}</p> : null}
            {error ? <p className="mt-3 text-sm text-pinkGlow">{error}</p> : null}
          </div>
        </div>
        <div className="relative overflow-hidden border-t border-white/10 bg-black/20 p-6 sm:p-8 lg:border-l lg:border-t-0 lg:p-8">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_22%_15%,rgba(0,229,255,0.14),transparent_34%),radial-gradient(circle_at_88%_82%,rgba(236,72,153,0.12),transparent_36%)]" />
          <div className="relative flex h-full flex-col justify-start gap-5 pt-8 lg:pt-12">
            <div className="rounded-3xl border border-white/10 bg-black/35 p-5 shadow-[0_24px_80px_rgba(0,0,0,0.24)]">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.24em] text-cyanGlow">AI coding assessment platform</p>
                  <h2 className="mt-3 text-2xl font-semibold leading-tight text-white">From sign-in to AI-aware submission in one flow.</h2>
                </div>
                <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-cyanGlow/20 bg-cyanGlow/10 px-3 py-1.5 text-xs font-semibold text-cyanGlow">
                  <span className="h-1.5 w-1.5 rounded-full bg-cyanGlow shadow-[0_0_12px_rgba(0,229,255,0.9)]" />
                  Live
                </span>
              </div>

              <div className="mt-5 grid gap-2.5">
                {assessmentHighlights.map(({ icon: Icon, title, copy }) => (
                  <article key={title} className="grid grid-cols-[auto_1fr] gap-3 rounded-2xl border border-white/10 bg-white/[0.055] p-3.5 transition hover:border-cyanGlow/25 hover:bg-white/[0.075]">
                    <span className="grid h-9 w-9 place-items-center rounded-xl bg-white/10 text-cyanGlow">
                      <Icon size={18} />
                    </span>
                    <span>
                      <span className="block text-sm font-semibold text-white">{title}</span>
                      <span className="mt-1 block text-sm leading-5 text-white/58">{copy}</span>
                    </span>
                  </article>
                ))}
              </div>

              <div className="mt-5 rounded-2xl border border-white/10 bg-white/[0.045] p-4 shadow-[0_18px_50px_rgba(0,0,0,0.18)]">
                <div className="flex items-center gap-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.22em] text-white/45">Sign-in notes</p>
                  <span className="h-px flex-1 bg-white/10" />
                </div>
                <div className="mt-3 grid gap-2">
                  {signInNotes.map(({ icon: Icon, copy }) => (
                    <p key={copy} className="grid grid-cols-[auto_1fr] items-start gap-2.5 text-sm leading-5 text-white/58">
                      <Icon size={15} className="mt-0.5 text-cyanGlow/70" />
                      <span>{copy}</span>
                    </p>
                  ))}
                </div>
                {showLocalDemoAccount ? (
                  <div className="mt-4 rounded-2xl border border-cyanGlow/20 bg-cyanGlow/[0.08] p-3.5">
                    <div className="flex items-center gap-3 text-sm font-semibold text-white">
                      <KeyRound size={17} className="text-cyanGlow" />
                      Local admin
                    </div>
                    <p className="mt-2 font-mono text-xs leading-6 text-white/70">
                      <span className="text-white">admin@example.com</span>
                      <span className="px-2 text-white/30">/</span>
                      <span className="text-white">Admin123!</span>
                    </p>
                  </div>
                ) : null}
              </div>
            </div>

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
