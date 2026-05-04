"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Code2, LogIn, UserPlus } from "lucide-react";
import { login, registerStudent } from "@/lib/api";

type AuthAction = "login" | "register";

export default function LoginPage() {
  const [email, setEmail] = useState("student@example.com");
  const [password, setPassword] = useState("password");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submittingAction, setSubmittingAction] = useState<AuthAction | null>(null);
  const router = useRouter();

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
    try {
      const fallbackName = email.split("@")[0]?.trim() || "Student";
      await registerStudent({ full_name: fallbackName, email, password });
      setNotice("Student account registered. You can sign in now.");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Registration failed.");
    } finally {
      setSubmittingAction(null);
    }
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
              Email
              <input className="field" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Password
              <input className="field" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={6} />
            </label>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submittingAction !== null}>
                <LogIn size={18} />
                {submittingAction === "login" ? "Connecting..." : "Sign in"}
              </button>
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
{`POST http://localhost:5140/api/v1/auth/login
POST http://localhost:5140/api/v1/auth/register
GET  http://localhost:5140/api/v1/auth/me
POST http://localhost:5140/api/v1/auth/logout

seed_admin = "admin@example.com";
demo_student = "student@example.com";
password = "password";

admin_users = "/api/v1/admin/users";`}
            </pre>
          </div>
          <div className="mt-6 grid gap-3 text-sm text-white/55">
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Self registration always creates a student account.</p>
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Use the seed admin to create additional administrator accounts.</p>
          </div>
        </div>
      </section>
    </main>
  );
}
