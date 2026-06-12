"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Loader2, Trash2 } from "lucide-react";
import { QuestionTestCaseEditor } from "@/components/admin/QuestionTestCaseEditor";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { deleteAssessment, getAdminAssessment, isAuthenticationError, updateAssessment } from "@/lib/api";
import type { Assessment, AssessmentStatus } from "@/lib/types";

export default function EditAssessmentPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    getAdminAssessment(assessmentId)
      .then((nextAssessment) => {
        setAssessment(nextAssessment);
        setError(null);
      })
      .catch((exception) => {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load assessment.");
      })
      .finally(() => setIsLoading(false));
  }, [assessmentId, router]);

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
      shared_prototype_reference: assessment.shared_prototype_reference ?? null,
      shared_prototype_version: assessment.shared_prototype_version ?? null,
      shared_prototype_metadata: {
        ...(assessment.shared_prototype_metadata ?? {}),
        student_setup: "platform_native",
        dependency_install_required: "false"
      }
    };
    setError(null);
    setSaved(false);
    setIsSaving(true);
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
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeleteAssessment() {
    if (!assessment) {
      return;
    }

    if (!window.confirm(`Delete "${assessment.title}" and all of its questions, attempts, submissions, and AI usage records?`)) {
      return;
    }

    setError(null);
    setSaved(false);
    setIsDeleting(true);
    try {
      await deleteAssessment(assessment.assessment_id);
      router.push("/admin/assessments");
    } catch (exception) {
      if (isAuthenticationError(exception)) {
        router.replace("/login");
        return;
      }

      setError(exception instanceof Error ? exception.message : "Unable to delete assessment.");
      setIsDeleting(false);
    }
  }

  if (isLoading) {
    return <SectionHeader eyebrow="Administrator" title="Connecting to backend..." />;
  }

  if (!assessment) {
    return <SectionHeader eyebrow="Administrator" title={error ?? "Assessment was not found."} />;
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
            <div className="flex flex-wrap gap-3">
              <button className="btn-primary" disabled={isSaving || isDeleting}>
                {isSaving ? <Loader2 className="animate-spin" size={16} /> : null}
                {isSaving ? "Saving in backend..." : "Save changes"}
              </button>
              <button className="btn-secondary text-pinkGlow" type="button" disabled={isSaving || isDeleting} onClick={handleDeleteAssessment}>
                {isDeleting ? <Loader2 className="animate-spin" size={16} /> : <Trash2 size={16} />}
                {isDeleting ? "Deleting..." : "Delete assessment"}
              </button>
            </div>
            {isSaving ? <p className="text-sm text-white/55" aria-live="polite">Waiting for backend confirmation before marking this assessment saved.</p> : null}
            {saved ? <p className="text-sm text-cyanGlow">Saved in backend.</p> : null}
            {error ? <p className="text-sm text-pinkGlow">{error}</p> : null}
          </form>
        </section>
        <QuestionTestCaseEditor assessment={assessment} onAssessmentChange={setAssessment} />
      </div>
    </div>
  );
}
