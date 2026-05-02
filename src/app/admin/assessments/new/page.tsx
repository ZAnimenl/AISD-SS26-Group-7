"use client";

import { useState } from "react";
import Link from "next/link";
import { createAssessmentMock } from "@/lib/mock-api";
import { SectionHeader } from "@/components/ui/SectionHeader";

export default function NewAssessmentPage() {
  const [saved, setSaved] = useState(false);

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title="Create assessment" />
      <section className="panel max-w-4xl">
        <form
          className="relative grid gap-5"
          onSubmit={(event) => {
            event.preventDefault();
            createAssessmentMock();
            setSaved(true);
          }}
        >
          <label className="grid gap-2 text-sm text-white/60">Title<input className="field" defaultValue="New Coding Assessment" /></label>
          <label className="grid gap-2 text-sm text-white/60">Description<textarea className="field min-h-28" defaultValue="Describe the assessment goals and rules for students." /></label>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="grid gap-2 text-sm text-white/60">Duration<input className="field" type="number" defaultValue={75} /></label>
            <label className="grid gap-2 text-sm text-white/60">Status<select className="field" defaultValue="draft"><option>draft</option><option>active</option><option>closed</option><option>archived</option></select></label>
            <label className="grid gap-2 text-sm text-white/60">AI assistance<select className="field" defaultValue="enabled"><option>enabled</option><option>disabled</option></select></label>
          </div>
          <div className="rounded-2xl border border-white/10 bg-black/20 p-4">
            <p className="text-sm font-semibold">Questions will be added after creation</p>
            <p className="mt-1 text-sm text-white/45">This MVP keeps form changes local and uses mock behavior only.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <button className="btn-primary" type="submit">Save mock assessment</button>
            <Link className="btn-secondary" href="/admin/assessments">Back to list</Link>
            {saved ? <span className="text-sm text-cyanGlow">Saved locally for demo.</span> : null}
          </div>
        </form>
      </section>
    </div>
  );
}
