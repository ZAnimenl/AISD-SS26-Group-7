"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Loader2, Wand2 } from "lucide-react";
import { createAssessment, generateAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";

type CreateMode = "manual" | "generate";

export default function NewAssessmentPage() {
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
            setPendingMode(nextMode);
            try {
              const create = shouldGenerate ? generateAssessment : createAssessment;
              const createdAssessment = await create({
                title: String(form.get("title") ?? ""),
                description: String(form.get("description") ?? ""),
                duration_minutes: Number(form.get("duration_minutes") ?? 75),
                status: (shouldGenerate ? "draft" : String(form.get("status") ?? "draft")) as any,
                ai_enabled: form.get("ai_enabled") === "enabled",
                shared_prototype_reference: null,
                shared_prototype_version: null
              });
              router.push(`/admin/assessments/${createdAssessment.assessment_id}#questions`);
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
            <p className="mt-1 text-sm text-white/45">Manual creation saves the assessment shell. LLM draft creation generates four review questions and keeps the assessment in draft.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <button className="btn-secondary" type="submit" name="creation_mode" value="generate" disabled={isPending}>
              {pendingMode === "generate" ? <Loader2 className="animate-spin" size={16} /> : <Wand2 size={16} />}
              {pendingMode === "generate" ? "Generating Questions..." : "Generate Questions"}
            </button>
            <button className="btn-primary" type="submit" name="creation_mode" value="manual" disabled={isPending}>
              {pendingMode === "manual" ? <Loader2 className="animate-spin" size={16} /> : null}
              {pendingMode === "manual" ? "Saving in backend..." : "Save assessment"}
            </button>
            <Link className={`btn-secondary ml-auto ${isPending ? "pointer-events-none opacity-45" : ""}`} href="/admin/assessments" aria-disabled={isPending}>Back to List</Link>
            {pendingMode ? (
              <span className="w-full text-sm text-white/55" aria-live="polite">
                {pendingMode === "generate"
                  ? "Backend is asking the configured AI provider for four real draft questions. Nothing is saved until the response is confirmed."
                  : "Saving assessment shell in the backend..."}
              </span>
            ) : null}
            {error ? <span className="w-full text-sm text-pinkGlow">{error}</span> : null}
          </div>
        </form>
      </section>
    </div>
  );
}
