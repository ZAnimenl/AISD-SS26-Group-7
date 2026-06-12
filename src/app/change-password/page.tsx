"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Code2, KeyRound, LogOut } from "lucide-react";
import { changePassword, getStoredUser, logout } from "@/lib/api";
import type { AuthUser } from "@/lib/types";

export default function ChangePasswordPage() {
  const router = useRouter();
  const [user] = useState<AuthUser | null>(() => getStoredUser());
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!user) {
      router.replace("/login");
    }
  }, [router, user]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    if (newPassword.length < 6) {
      setError("New password must be at least 6 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }
    setSubmitting(true);
    try {
      // The user just signed in with their temporary password, so the backend
      // accepts a change-password request without re-asking for it. We still
      // pass an empty string so the request body shape stays the same.
      await changePassword("", newPassword);
      setNotice("Password updated. Redirecting...");
      setTimeout(() => {
        router.replace(user?.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
      }, 800);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Could not update the password.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleLogout() {
    await logout();
    router.replace("/login");
  }

  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon mx-auto w-full max-w-md rounded-3xl p-10">
        <span className="inline-grid h-12 w-12 place-items-center rounded-2xl bg-cyanGlow/10 text-cyanGlow">
          <Code2 size={22} />
        </span>
        <h1 className="mt-6 font-heading text-3xl italic text-white">Set a new password</h1>
        <p className="mt-3 text-sm text-white/60">
          You signed in with a temporary password. Pick a new one to continue.
        </p>

        <form onSubmit={handleSubmit} className="mt-8 grid gap-4">
          <label className="grid gap-2 text-sm text-white/60">
            New password
            <input
              className="field"
              type="password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              required
              minLength={6}
              autoComplete="new-password"
              autoFocus
            />
          </label>
          <label className="grid gap-2 text-sm text-white/60">
            Confirm new password
            <input
              className="field"
              type="password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              required
              minLength={6}
              autoComplete="new-password"
            />
          </label>
          <div className="flex flex-wrap gap-2 pt-2">
            <button className="btn-primary" type="submit" disabled={submitting}>
              <KeyRound size={18} />
              {submitting ? "Updating..." : "Update password"}
            </button>
            <button className="btn-secondary" type="button" onClick={handleLogout}>
              <LogOut size={18} />
              Sign out
            </button>
          </div>
        </form>

        {notice ? <p className="mt-4 text-sm text-cyanGlow">{notice}</p> : null}
        {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
      </section>
    </main>
  );
}
