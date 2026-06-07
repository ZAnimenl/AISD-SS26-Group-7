"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { createAssessment, generateAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";

type CreateMode = "manual" | "generate";

export default function NewAssessmentPage() {
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingMode, setPendingMode] = useState<CreateMode | null>(null);
  const router = useRouter();
  const isPending = pendingMode !== null;

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Create assessment" />
      <section className="panel max-w-4xl">
        <form
          className="relative grid gap-5"
          onSubmit={async (event) => {
            event.preventDefault();
            const form = new FormData(event.currentTarget);
            const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
            const nextMode: CreateMode = submitter?.value === "generate" ? "generate" : "manual";
            const shouldGenerate = nextMode === "generate";
            setError(null);
            setSaved(false);
            setPendingMode(nextMode);
            try {
              const create = shouldGenerate ? generateAssessment : createAssessment;
              await create({
                title: String(form.get("title") ?? ""),
                description: String(form.get("description") ?? ""),
                duration_minutes: Number(form.get("duration_minutes") ?? 75),
                status: (shouldGenerate ? "draft" : String(form.get("status") ?? "draft")) as any,
                ai_enabled: form.get("ai_enabled") === "enabled",
                shared_prototype_reference: null,
                shared_prototype_version: null
              });
              setSaved(true);
            } catch (exception) {
              setError(exception instanceof Error ? exception.message : "Unable to create assessment.");
            } finally {
              setPendingMode(null);
            }
          }}
        >
          <label className="grid gap-2 text-sm text-white/60">Title<input className="field" name="title" required /></label>
          <label className="grid gap-2 text-sm text-white/60">Description<textarea className="field min-h-28" name="description" required /></label>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="grid gap-2 text-sm text-white/60">Duration<input className="field" name="duration_minutes" type="number" defaultValue={75} /></label>
            <label className="grid gap-2 text-sm text-white/60">Status<select className="field" name="status" defaultValue="draft"><option>draft</option><option>active</option><option>closed</option><option>archived</option></select></label>
            <label className="grid gap-2 text-sm text-white/60">AI assistance<select className="field" name="ai_enabled" defaultValue="enabled"><option>enabled</option><option>disabled</option></select></label>
          </div>
          <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
            <p className="text-sm font-semibold">Questions will be added after creation</p>
            <p className="mt-1 text-sm text-white/45">Manual creation saves the assessment shell. LLM draft creation calls the configured backend AI provider and keeps the assessment in draft for review.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <button className="btn-primary" type="submit" name="creation_mode" value="manual" disabled={isPending}>
              {pendingMode === "manual" ? <Loader2 className="animate-spin" size={16} /> : null}
              {pendingMode === "manual" ? "Saving in backend..." : "Save assessment"}
            </button>
            <button className="btn-secondary" type="submit" name="creation_mode" value="generate" disabled={isPending}>
              {pendingMode === "generate" ? <Loader2 className="animate-spin" size={16} /> : null}
              {pendingMode === "generate" ? "Waiting for AI draft..." : "Generate LLM draft"}
            </button>
            <Link className={`btn-secondary ${isPending ? "pointer-events-none opacity-45" : ""}`} href="/admin/assessments" aria-disabled={isPending}>Back to list</Link>
            {saved ? <button className="btn-secondary" type="button" onClick={() => router.push("/admin/assessments")} disabled={isPending}>View list</button> : null}
            {pendingMode ? (
              <span className="text-sm text-white/55" aria-live="polite">
                {pendingMode === "generate"
                  ? "Backend is asking the configured AI provider for a real draft. Nothing is saved until the response is confirmed."
                  : "Saving assessment shell in the backend..."}
              </span>
            ) : null}
            {saved ? <span className="text-sm text-cyanGlow">Saved in backend.</span> : null}
            {error ? <span className="text-sm text-pinkGlow">{error}</span> : null}
          </div>
        </form>
      </section>
    </div>
  );
}
