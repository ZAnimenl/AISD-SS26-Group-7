"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { QuestionTestCaseEditor } from "@/components/admin/QuestionTestCaseEditor";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { getAdminAssessment, isAuthenticationError, updateAssessment } from "@/lib/api";
import type { Assessment, AssessmentStatus } from "@/lib/types";

export default function EditAssessmentPage({ params }: { params: { assessmentId: string } }) {
  const router = useRouter();
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAdminAssessment(params.assessmentId)
      .then(setAssessment)
      .catch((exception) => {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load assessment.");
      });
  }, [params.assessmentId, router]);

  async function saveAssessment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!assessment) {
      return;
    }

    const form = new FormData(event.currentTarget);
    const nextAssessment = {
      ...assessment,
      title: String(form.get("title") ?? assessment.title),
      description: String(form.get("description") ?? assessment.description),
      duration_minutes: Number(form.get("duration_minutes") ?? assessment.duration_minutes),
      status: String(form.get("status") ?? assessment.status) as AssessmentStatus,
      ai_enabled: form.get("ai_enabled") === "enabled",
      shared_prototype_reference: String(form.get("shared_prototype_reference") ?? assessment.shared_prototype_reference ?? "").trim() || null,
      shared_prototype_version: String(form.get("shared_prototype_version") ?? assessment.shared_prototype_version ?? "").trim() || null,
      shared_prototype_metadata: {
        ...(assessment.shared_prototype_metadata ?? {}),
        student_setup: "platform_native",
        dependency_install_required: "false"
      }
    };
    setError(null);
    try {
      await updateAssessment(nextAssessment);
      setAssessment(nextAssessment);
      setSaved(true);
    } catch (exception) {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to save assessment.");
    }
  }

  if (!assessment) {
    return <SectionHeader eyebrow="Administrator" title="Connecting to backend..." />;
  }

  return (
    <div>
      <SectionHeader eyebrow="Administrator" title={`Edit ${assessment.title}`} />
      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <section className="panel">
          <form className="relative grid gap-4" onSubmit={saveAssessment}>
            <div className="flex items-center justify-between"><h2 className="text-lg font-semibold">Assessment details</h2><StatusBadge status={assessment.status} /></div>
            <label className="grid gap-2 text-sm text-white/60">Title<input className="field" name="title" defaultValue={assessment.title} /></label>
            <label className="grid gap-2 text-sm text-white/60">Description<textarea className="field min-h-28" name="description" defaultValue={assessment.description} /></label>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="grid gap-2 text-sm text-white/60">Duration<input className="field" name="duration_minutes" type="number" defaultValue={assessment.duration_minutes} /></label>
              <label className="grid gap-2 text-sm text-white/60">Status<select className="field" name="status" defaultValue={assessment.status}><option>draft</option><option>active</option><option>closed</option><option>archived</option></select></label>
            </div>
            <label className="grid gap-2 text-sm text-white/60">AI assistance<select className="field" name="ai_enabled" defaultValue={assessment.ai_enabled ? "enabled" : "disabled"}><option>enabled</option><option>disabled</option></select></label>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="grid gap-2 text-sm text-white/60">Shared prototype reference<input className="field" name="shared_prototype_reference" defaultValue={assessment.shared_prototype_reference ?? ""} /></label>
              <label className="grid gap-2 text-sm text-white/60">Shared prototype version<input className="field" name="shared_prototype_version" defaultValue={assessment.shared_prototype_version ?? ""} /></label>
            </div>
            <button className="btn-primary w-fit">Save changes</button>
            {saved ? <p className="text-sm text-cyanGlow">Saved in backend.</p> : null}
            {error ? <p className="text-sm text-pinkGlow">{error}</p> : null}
          </form>
        </section>
        <QuestionTestCaseEditor assessment={assessment} onAssessmentChange={setAssessment} />
      </div>
    </div>
  );
}
