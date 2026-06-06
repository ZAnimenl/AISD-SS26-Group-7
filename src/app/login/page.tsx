"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Code2, KeyRound, LogIn, UserPlus } from "lucide-react";
import { getStoredUser, login, registerStudent } from "@/lib/api";

type AuthAction = "login" | "register";
const localDemoAdmin = {
  email: "admin@example.com",
  password: "Admin123!"
};
const showLocalDemoAccount = process.env.NODE_ENV !== "production";

export default function LoginPage() {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submittingAction, setSubmittingAction] = useState<AuthAction | null>(null);
  const router = useRouter();

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
      const user = await login(email, password);
      router.push(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Authentication failed.");
    } finally {
      setSubmittingAction(null);
    }
  }

  async function handleRegisterStudent() {
    setError(null);
    setNotice(null);
    setSubmittingAction("register");
    if (!fullName.trim()) {
      setError("Full name is required to register a student account.");
      setSubmittingAction(null);
      return;
    }

    try {
      await registerStudent({ full_name: fullName.trim(), email, password });
      setNotice("Student account registered. You can sign in now.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Registration failed.");
    } finally {
      setSubmittingAction(null);
    }
  }

  function fillLocalAdminAccount() {
    setEmail(localDemoAdmin.email);
    setPassword(localDemoAdmin.password);
    setFullName("");
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
              <span className="block text-xs text-white/45">Frontend and API client</span>
            </span>
          </div>
          <h1 className="font-heading text-5xl italic leading-tight text-white lg:text-7xl">Enter the assessment workspace.</h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-white/60">
            Sign in with a backend account, or register a student account. Administrator accounts are created by an administrator.
          </p>
          <form className="mt-10 grid max-w-xl gap-4" onSubmit={handleSignIn}>
            <label className="grid gap-2 text-sm text-white/60">
              Full name
              <input className="field" type="text" value={fullName} onChange={(event) => setFullName(event.target.value)} autoComplete="name" />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Email
              <input className="field" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required autoComplete="email" />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Password
              <input className="field" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={6} autoComplete="current-password" />
            </label>
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
              <button className="btn-secondary" type="button" onClick={handleRegisterStudent} disabled={submittingAction !== null}>
                <UserPlus size={18} />
                {submittingAction === "register" ? "Creating..." : "Register Student"}
              </button>
            </div>
          </form>
          {notice ? <p className="mt-4 text-sm text-cyanGlow">{notice}</p> : null}
          {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
        </div>
        <div className="border-t border-white/10 bg-black/20 p-8 lg:border-l lg:border-t-0 lg:p-12">
          <div className="rounded-3xl border border-white/10 bg-black/30 p-5 font-mono text-sm text-white/70">
            <div className="mb-4 flex gap-2">
              <span className="h-3 w-3 rounded-full bg-pinkGlow" />
              <span className="h-3 w-3 rounded-full bg-purpleGlow" />
              <span className="h-3 w-3 rounded-full bg-cyanGlow" />
            </div>
            <pre className="whitespace-pre-wrap leading-7">
{`POST /api/v1/auth/login
POST /api/v1/auth/register
GET  /api/v1/auth/me
POST /api/v1/auth/logout

admin_users = "/api/v1/admin/users";`}
            </pre>
          </div>
          <div className="mt-6 grid gap-3 text-sm text-white/55">
            {showLocalDemoAccount ? (
              <p className="rounded-2xl border border-white/10 bg-white/5 p-4">
                Local admin: <span className="text-white">admin@example.com</span> / <span className="text-white">Admin123!</span>
              </p>
            ) : null}
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Self registration always creates a student account.</p>
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Administrator accounts are created from configured backend credentials or by existing administrators.</p>
          </div>
        </div>
      </section>
    </main>
  );
}
