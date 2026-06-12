"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Code2, Mail, LogIn } from "lucide-react";
import { forgotPassword } from "@/lib/api";

export default function ForgotPasswordPage() {
  return (
    <Suspense fallback={<ForgotPasswordShell />}>
      <ForgotPasswordContent />
    </Suspense>
  );
}

function ForgotPasswordContent() {
  const params = useSearchParams();
  const [email, setEmail] = useState(() => params.get("email") ?? "");
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const [devTemp, setDevTemp] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setDone(false);
    setDevTemp(null);
    if (!email.includes("@")) {
      setError("Please enter a valid email address.");
      return;
    }
    setSubmitting(true);
    try {
      const result = await forgotPassword(email.trim());
      setDone(true);
      setDevTemp(result.devTemporaryPassword);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Could not send the reset email.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon mx-auto w-full max-w-md rounded-3xl p-10">
        <span className="inline-grid h-12 w-12 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow">
          <Code2 size={22} />
        </span>
        <h1 className="mt-6 font-heading text-3xl italic text-white">Forgot your password?</h1>
        <p className="mt-3 text-sm text-white/60">
          Enter the email you registered with. If we find an account, we&apos;ll send a temporary
          password you can use to sign in. You will be asked to set a new password right after.
        </p>

        {done ? (
          <div className="mt-8 grid gap-4">
            <div className="rounded-2xl border border-cyanGlow/30 bg-cyanGlow/10 p-5 text-sm text-white">
              <p className="font-semibold text-cyanGlow">Check your inbox.</p>
              <p className="mt-2 text-white/75">
                If <span className="font-mono text-white">{email}</span> is registered, we just sent it a temporary password. It expires in 30 minutes.
              </p>
              {devTemp ? (
                <p className="mt-3 text-xs text-amber-300">
                  Dev fallback (email delivery is off): <span className="font-mono text-white">{devTemp}</span>
                </p>
              ) : null}
            </div>
            <Link className="btn-primary" href={`/login?email=${encodeURIComponent(email)}`}>
              <LogIn size={18} />
              Go to sign in
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="mt-8 grid gap-4">
            <label className="grid gap-2 text-sm text-white/60">
              Email
              <input
                className="field"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                autoComplete="email"
                required
              />
            </label>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submitting}>
                <Mail size={18} />
                {submitting ? "Sending..." : "Send temporary password"}
              </button>
              <Link className="btn-secondary" href="/login">
                <ArrowLeft size={18} />
                Back to sign in
              </Link>
            </div>
          </form>
        )}

        {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
      </section>
    </main>
  );
}

function ForgotPasswordShell() {
  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon mx-auto w-full max-w-md rounded-3xl p-10">
        <span className="inline-grid h-12 w-12 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow">
          <Code2 size={22} />
        </span>
        <h1 className="mt-6 font-heading text-3xl italic text-white">Forgot your password?</h1>
        <p className="mt-3 text-sm text-white/60">Loading reset form...</p>
      </section>
    </main>
  );
}
