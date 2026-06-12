"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Code2, Mail, ShieldCheck, KeyRound, LogIn } from "lucide-react";
import {
  ApiRequestError,
  registerStart,
  registerVerifyCode,
  registerComplete,
  registerResendCode
} from "@/lib/api";

type Step = "details" | "code" | "password";

export default function RegisterPage() {
  const router = useRouter();
  const [step, setStep] = useState<Step>("details");

  // Step 1: details
  const [fullName, setFullName] = useState("");
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");

  // Step 2: code
  const [code, setCode] = useState("");
  const [codeExpiresAt, setCodeExpiresAt] = useState<string | null>(null);
  const [devCode, setDevCode] = useState<string | null>(null);
  const [resending, setResending] = useState(false);

  // Step 3: password
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);

  // Shared UI state
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [emailTakenFor, setEmailTakenFor] = useState<string | null>(null);
  const [usernameTakenFor, setUsernameTakenFor] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const codeInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (step === "code") {
      codeInputRef.current?.focus();
    }
  }, [step]);

  async function handleSendCode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setEmailTakenFor(null);
    setUsernameTakenFor(null);
    if (!fullName.trim()) {
      setError("Please enter your full name.");
      return;
    }
    if (username.trim().length < 3) {
      setError("Please enter a username with at least 3 characters.");
      return;
    }
    if (!email.includes("@")) {
      setError("Please enter a valid email address.");
      return;
    }
    setSubmitting(true);
    try {
      const result = await registerStart({
        full_name: fullName.trim(),
        username: username.trim(),
        email: email.trim()
      });
      setCodeExpiresAt(result.expiresAt);
      setDevCode(result.devCode);
      setNotice(result.sent
        ? `We sent a 6-digit code to ${email}. It expires in 15 minutes.`
        : result.devCode
          ? `Email delivery is offline. Use this code: ${result.devCode}`
          : "Code generated, but the email was not sent.");
      setStep("code");
    } catch (exception) {
      if (exception instanceof ApiRequestError && exception.code === "EMAIL_TAKEN") {
        setEmailTakenFor(email.trim());
        setError(null);
      } else if (exception instanceof ApiRequestError && exception.code === "USERNAME_TAKEN") {
        setUsernameTakenFor(username.trim());
        setError(null);
      } else {
        setError(exception instanceof Error ? exception.message : "Could not send the verification code.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleVerifyCode(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    if (code.trim().length !== 6) {
      setError("Please enter the 6-digit code from your email.");
      return;
    }
    setSubmitting(true);
    try {
      await registerVerifyCode({ email: email.trim(), code: code.trim() });
      setNotice("Code accepted. Now create a password.");
      setStep("password");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "That code is not correct.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleResendCode() {
    setError(null);
    setNotice(null);
    setResending(true);
    try {
      const result = await registerResendCode(email.trim());
      setCodeExpiresAt(result.expiresAt);
      setDevCode(result.devCode);
      setNotice(result.sent
        ? `New code sent to ${email}.`
        : result.devCode
          ? `Email delivery is offline. Use this code: ${result.devCode}`
          : "New code generated, but the email was not sent.");
      setCode("");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Could not resend the code.");
    } finally {
      setResending(false);
    }
  }

  async function handleCreateAccount(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setNotice(null);
    if (password.length < 6) {
      setError("Password must be at least 6 characters.");
      return;
    }
    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }
    setSubmitting(true);
    try {
      const user = await registerComplete({
        email: email.trim(),
        code: code.trim(),
        password,
        rememberMe
      });
      router.replace(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Could not create the account.");
    } finally {
      setSubmitting(false);
    }
  }

  function goBackToDetails() {
    setError(null);
    setNotice(null);
    setStep("details");
    setCode("");
  }

  function goBackToCode() {
    setError(null);
    setNotice(null);
    setStep("code");
  }

  return (
    <main className="page-shell bg-grid grid min-h-screen place-items-center p-4">
      <section className="liquid-glass-neon mx-auto w-full max-w-xl rounded-3xl p-8 lg:p-12">
        <div className="mb-8 inline-flex items-center gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-cyanGlow/10 text-cyanGlow">
            <Code2 size={19} />
          </span>
          <span>
            <span className="block text-sm font-semibold">AI Coding Assessment</span>
            <span className="block text-xs text-white/45">Create your student account</span>
          </span>
        </div>

        <StepIndicator step={step} />

        {step === "details" ? (
          <form onSubmit={handleSendCode} className="mt-8 grid gap-4">
            <label className="grid gap-2 text-sm text-white/60">
              Full name
              <input
                className="field"
                type="text"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                autoComplete="name"
                required
              />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Username
              <input
                className="field"
                type="text"
                value={username}
                onChange={(event) => {
                  setUsername(event.target.value);
                  if (usernameTakenFor && event.target.value !== usernameTakenFor) {
                    setUsernameTakenFor(null);
                  }
                }}
                autoComplete="username"
                required
                minLength={3}
              />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Email
              <input
                className="field"
                type="email"
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value);
                  if (emailTakenFor && event.target.value !== emailTakenFor) {
                    setEmailTakenFor(null);
                  }
                }}
                autoComplete="email"
                required
              />
            </label>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submitting}>
                <Mail size={18} />
                {submitting ? "Sending code..." : "Send verification code"}
              </button>
              <Link className="btn-secondary" href="/login">
                <ArrowLeft size={18} />
                Back to sign in
              </Link>
            </div>

            {emailTakenFor ? (
              <div className="mt-6 grid gap-4 rounded-2xl border border-pinkGlow/40 bg-pinkGlow/10 p-5 text-sm text-white">
                <p className="text-base font-semibold text-pinkGlow">This email is already registered.</p>
                <p className="text-white/75">
                  <span className="font-mono text-white">{emailTakenFor}</span> already has an account. Sign in with that account or use a different email.
                </p>
                <div className="flex flex-wrap gap-2">
                  <Link
                    className="btn-primary"
                    href={`/login?email=${encodeURIComponent(emailTakenFor)}`}
                  >
                    <LogIn size={18} />
                    Sign in instead
                  </Link>
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={() => {
                      setEmailTakenFor(null);
                      setEmail("");
                    }}
                  >
                    Use a different email
                  </button>
                </div>
              </div>
            ) : null}
            {usernameTakenFor ? (
              <div className="mt-6 grid gap-4 rounded-2xl border border-pinkGlow/40 bg-pinkGlow/10 p-5 text-sm text-white">
                <p className="text-base font-semibold text-pinkGlow">This username is already taken.</p>
                <p className="text-white/75">
                  <span className="font-mono text-white">{usernameTakenFor}</span> is already used by another account. Choose a different username to continue.
                </p>
                <button
                  type="button"
                  className="btn-secondary w-fit"
                  onClick={() => {
                    setUsernameTakenFor(null);
                    setUsername("");
                  }}
                >
                  Choose another username
                </button>
              </div>
            ) : null}
          </form>
        ) : null}

        {step === "code" ? (
          <form onSubmit={handleVerifyCode} className="mt-8 grid gap-4">
            <p className="text-sm text-white/70">
              We sent a 6-digit code to <span className="text-white">{email}</span>. Enter it below to confirm your email.
            </p>
            <label className="grid gap-2 text-sm text-white/60">
              Verification code
              <input
                ref={codeInputRef}
                className="field text-center text-2xl tracking-[0.5em] font-mono"
                type="text"
                inputMode="numeric"
                pattern="\d{6}"
                maxLength={6}
                value={code}
                onChange={(event) => setCode(event.target.value.replace(/\D/g, "").slice(0, 6))}
                required
                autoComplete="one-time-code"
              />
            </label>
            <CodeExpiry expiresAt={codeExpiresAt} />
            {devCode ? (
              <p className="text-xs text-amber-300">Dev fallback code (email delivery is off): <span className="font-mono text-white">{devCode}</span></p>
            ) : null}
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submitting}>
                <ShieldCheck size={18} />
                {submitting ? "Verifying..." : "Verify code"}
              </button>
              <button className="btn-secondary" type="button" onClick={handleResendCode} disabled={resending || submitting}>
                {resending ? "Resending..." : "Resend code"}
              </button>
              <button className="btn-secondary" type="button" onClick={goBackToDetails} disabled={submitting}>
                <ArrowLeft size={18} />
                Use a different email
              </button>
            </div>
          </form>
        ) : null}

        {step === "password" ? (
          <form onSubmit={handleCreateAccount} className="mt-8 grid gap-4">
            <p className="text-sm text-white/70">
              Username <span className="text-white">{username}</span> and email <span className="text-white">{email}</span> confirmed. Create a password to finish signing up.
            </p>
            <label className="grid gap-2 text-sm text-white/60">
              Create password
              <input
                className="field"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
                minLength={6}
                autoComplete="new-password"
              />
            </label>
            <label className="grid gap-2 text-sm text-white/60">
              Confirm password
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
            <label className="mt-1 flex cursor-pointer items-center gap-2 text-sm text-white/70 select-none">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(event) => setRememberMe(event.target.checked)}
                className="h-4 w-4 rounded border-white/20 bg-white/5 text-cyanGlow accent-cyanGlow"
              />
              Remember me on this device
            </label>
            <div className="flex flex-wrap gap-2 pt-2">
              <button className="btn-primary" type="submit" disabled={submitting}>
                <KeyRound size={18} />
                {submitting ? "Creating account..." : "Create account"}
              </button>
              <button className="btn-secondary" type="button" onClick={goBackToCode} disabled={submitting}>
                <ArrowLeft size={18} />
                Back
              </button>
            </div>
          </form>
        ) : null}

        {notice ? <p className="mt-4 text-sm text-cyanGlow">{notice}</p> : null}
        {error ? <p className="mt-4 text-sm text-pinkGlow">{error}</p> : null}
      </section>
    </main>
  );
}

function StepIndicator({ step }: { step: Step }) {
  const steps: { id: Step; label: string }[] = [
    { id: "details", label: "Details" },
    { id: "code", label: "Verify" },
    { id: "password", label: "Password" }
  ];
  const currentIndex = steps.findIndex((entry) => entry.id === step);
  return (
    <div className="flex items-center gap-3 text-xs uppercase tracking-wider text-white/40">
      {steps.map((entry, index) => {
        const isDone = index < currentIndex;
        const isActive = index === currentIndex;
        return (
          <span key={entry.id} className="flex items-center gap-2">
            <span className={`grid h-6 w-6 place-items-center rounded-full text-[11px] ${
              isActive
                ? "bg-cyanGlow text-[#0a0f1a] font-bold"
                : isDone
                  ? "bg-cyanGlow/20 text-cyanGlow"
                  : "bg-white/5 text-white/40"
            }`}>{index + 1}</span>
            <span className={isActive ? "text-white" : ""}>{entry.label}</span>
            {index < steps.length - 1 ? <span className="text-white/20">·</span> : null}
          </span>
        );
      })}
    </div>
  );
}

function CodeExpiry({ expiresAt }: { expiresAt: string | null }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);
  const remaining = useMemo(() => {
    if (!expiresAt) return null;
    const ms = new Date(expiresAt).getTime() - now;
    if (ms <= 0) return "expired";
    const totalSeconds = Math.floor(ms / 1000);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds.toString().padStart(2, "0")}`;
  }, [expiresAt, now]);
  if (!expiresAt || !remaining) return null;
  return (
    <p className="text-xs text-white/50">
      {remaining === "expired" ? "Code expired. Tap Resend to get a new one." : `Code expires in ${remaining}`}
    </p>
  );
}
