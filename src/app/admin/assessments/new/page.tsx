"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { createAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";

export default function NewAssessmentPage() {
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Create assessment" />
      <section className="panel max-w-4xl">
        <form
          className="relative grid gap-5"
          onSubmit={async (event) => {
            event.preventDefault();
            const form = new FormData(event.currentTarget);
            setError(null);
            try {
              await createAssessment({
                title: String(form.get("title") ?? ""),
                description: String(form.get("description") ?? ""),
                duration_minutes: Number(form.get("duration_minutes") ?? 75),
                status: String(form.get("status") ?? "draft") as any,
                ai_enabled: form.get("ai_enabled") === "enabled"
              });
              setSaved(true);
            } catch (exception) {
              setError(exception instanceof Error ? exception.message : "Unable to create assessment.");
            }
          }}
        >
          <label className="grid gap-2 text-sm text-white/60">Title<input className="field" name="title" defaultValue="New Coding Assessment" /></label>
          <label className="grid gap-2 text-sm text-white/60">Description<textarea className="field min-h-28" name="description" defaultValue="Describe the assessment goals and rules for students." /></label>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="grid gap-2 text-sm text-white/60">Duration<input className="field" name="duration_minutes" type="number" defaultValue={75} /></label>
            <label className="grid gap-2 text-sm text-white/60">Status<select className="field" name="status" defaultValue="draft"><option>draft</option><option>active</option><option>closed</option><option>archived</option></select></label>
            <label className="grid gap-2 text-sm text-white/60">AI assistance<select className="field" name="ai_enabled" defaultValue="enabled"><option>enabled</option><option>disabled</option></select></label>
          </div>
          <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
            <p className="text-sm font-semibold">Questions will be added after creation</p>
            <p className="mt-1 text-sm text-white/45">The assessment is saved through the backend API.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <button className="btn-primary" type="submit">Save assessment</button>
            <Link className="btn-secondary" href="/admin/assessments">Back to list</Link>
            {saved ? <button className="btn-secondary" type="button" onClick={() => router.push("/admin/assessments")}>View list</button> : null}
            {saved ? <span className="text-sm text-cyanGlow">Saved in backend.</span> : null}
            {error ? <span className="text-sm text-pinkGlow">{error}</span> : null}
          </div>
        </form>
      </section>
    </div>
  );
}
