"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Code2, ShieldCheck, Sparkles } from "lucide-react";
import { mockLogin } from "@/lib/mock-api";
import type { Role } from "@/lib/types";

export default function LoginPage() {
  const [role, setRole] = useState<Role>("student");
  const router = useRouter();

  function handleLogin() {
    const user = mockLogin(role);
    router.push(user.role === "administrator" ? "/admin/dashboard" : "/student/dashboard");
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
              <span className="block text-xs text-white/45">Module 2 visual MVP</span>
            </span>
          </div>
          <h1 className="font-heading text-5xl italic leading-tight text-white lg:text-7xl">Enter the assessment workspace.</h1>
          <p className="mt-5 max-w-xl text-lg leading-8 text-white/60">
            Choose a mock role to preview student and administrator flows. This screen does not create real auth tokens or sessions.
          </p>
          <div className="mt-10 grid gap-3 sm:grid-cols-2">
            {(["student", "administrator"] as Role[]).map((item) => (
              <button
                key={item}
                className={`rounded-2xl border p-5 text-left transition ${
                  role === item ? "border-cyanGlow/50 bg-cyanGlow/10" : "border-white/10 bg-white/5 hover:bg-white/10"
                }`}
                onClick={() => setRole(item)}
              >
                <ShieldCheck className={role === item ? "text-cyanGlow" : "text-white/45"} size={22} />
                <span className="mt-4 block text-lg font-semibold capitalize">{item === "administrator" ? "Administrator" : "Student"}</span>
                <span className="mt-1 block text-sm text-white/45">
                  {item === "administrator" ? "Author assessments and inspect reports" : "Open assessments and code in the browser IDE"}
                </span>
              </button>
            ))}
          </div>
          <button className="btn-primary mt-8" onClick={handleLogin}>
            <Sparkles size={18} />
            Continue in mock mode
          </button>
        </div>
        <div className="border-t border-white/10 bg-black/20 p-8 lg:border-l lg:border-t-0 lg:p-12">
          <div className="rounded-3xl border border-white/10 bg-black/30 p-5 font-mono text-sm text-white/70">
            <div className="mb-4 flex gap-2">
              <span className="h-3 w-3 rounded-full bg-pinkGlow" />
              <span className="h-3 w-3 rounded-full bg-purpleGlow" />
              <span className="h-3 w-3 rounded-full bg-cyanGlow" />
            </div>
            <pre className="whitespace-pre-wrap leading-7">
{`// TODO(API): POST /api/v1/auth/login
// TODO(API): GET /api/v1/auth/me
// TODO(API): POST /api/v1/auth/logout

auth.mode = "mock role selection";
session_id = undefined;`}
            </pre>
          </div>
          <div className="mt-6 grid gap-3 text-sm text-white/55">
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">No real JWT handling in this frontend MVP.</p>
            <p className="rounded-2xl border border-white/10 bg-white/5 p-4">Backend will later resolve identity and attempts from secure auth context.</p>
          </div>
        </div>
      </section>
    </main>
  );
}
