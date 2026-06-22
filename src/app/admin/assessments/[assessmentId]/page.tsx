"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Loader2, Trash2 } from "lucide-react";
import { QuestionTestCaseEditor } from "@/components/admin/QuestionTestCaseEditor";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { CustomDropdown } from "@/components/ui/CustomDropdown";
import { DurationSlider } from "@/components/admin/DurationSlider";
import { deleteAssessment, getAdminAssessment, isAuthenticationError, updateAssessment } from "@/lib/api";
import { currentUtcIso, defaultAssessmentExpiry, toLocalDateTimeInput, toUtcIso } from "@/lib/assessmentSchedule";
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
  const [startMode, setStartMode] = useState<"now" | "scheduled">("now");
  const [scheduledStart, setScheduledStart] = useState(() => toLocalDateTimeInput());
  const [expiresAt, setExpiresAt] = useState(defaultAssessmentExpiry);
  const [statusValue, setStatusValue] = useState<AssessmentStatus>("draft");
  const [aiAccess, setAiAccess] = useState<"enabled" | "disabled">("enabled");
  const [durationMinutes, setDurationMinutes] = useState(50);

  useEffect(() => {
    getAdminAssessment(assessmentId)
      .then((nextAssessment) => {
        setAssessment(nextAssessment);
        setStartMode(nextAssessment.starts_at ? "scheduled" : "now");
        setScheduledStart(toLocalDateTimeInput(nextAssessment.starts_at));
        setExpiresAt(nextAssessment.expires_at ? toLocalDateTimeInput(nextAssessment.expires_at) : defaultAssessmentExpiry());
        setStatusValue(nextAssessment.status);
        setAiAccess(nextAssessment.ai_enabled ? "enabled" : "disabled");
        setDurationMinutes(Math.max(1, nextAssessment.duration_minutes));
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
    if (!Number.isInteger(durationMinutes) || durationMinutes <= 0) {
      setError("Duration must be a whole number greater than zero.");
      return;
    }
    const submittedStart = String(form.get("starts_at") ?? scheduledStart);
    const submittedExpiry = String(form.get("expires_at") ?? expiresAt);
    const startsAt = startMode === "scheduled" ? toUtcIso(submittedStart) : currentUtcIso();
    const expiry = toUtcIso(submittedExpiry);
    if (!expiry || new Date(expiry).getTime() <= new Date(startsAt ?? currentUtcIso()).getTime()) {
      setError("Assessment expiration must be later than its start time.");
      return;
    }
    const nextAssessment = {
      ...assessment,
      title: String(form.get("title") ?? assessment.title),
      description: String(form.get("description") ?? assessment.description),
      duration_minutes: durationMinutes,
      starts_at: startsAt,
      expires_at: expiry,
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
    return <SectionHeader eyebrow="Administrator" title="Loading assessment..." />;
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
              <DurationSlider value={durationMinutes} onChange={setDurationMinutes} disabled={isSaving || isDeleting} />
              <div className="grid gap-2 text-sm text-white/60">
                Starts
                <div className="grid grid-cols-2 rounded-xl border border-white/10 bg-black/20 p-1">
                  {(["now", "scheduled"] as const).map((mode) => (
                    <button
                      key={mode}
                      type="button"
                      className={`rounded-lg px-3 py-2 text-sm font-medium transition ${
                        startMode === mode ? "bg-cyanGlow/15 text-cyanGlow" : "text-white/45 hover:text-white/75"
                      }`}
                      aria-pressed={startMode === mode}
                      onClick={() => setStartMode(mode)}
                    >
                      {mode === "now" ? "Now" : "Schedule"}
                    </button>
                  ))}
                </div>
              </div>
              {startMode === "scheduled" ? (
                <label className="grid gap-2 text-sm text-white/60 sm:col-span-2">
                  Start date and time
                  <input className="field [color-scheme:dark]" name="starts_at" type="datetime-local" value={scheduledStart} required onChange={(event) => setScheduledStart(event.target.value)} />
                </label>
              ) : null}
              <label className="grid gap-2 text-sm text-white/60 sm:col-span-2">
                Assessment expires
                <input className="field [color-scheme:dark]" name="expires_at" type="datetime-local" value={expiresAt} required onChange={(event) => setExpiresAt(event.target.value)} />
                <span className="text-xs text-white/35">After this deadline students can review results, but cannot start or continue an attempt.</span>
              </label>
              <label className="grid gap-2 text-sm text-white/60">Status<CustomDropdown name="status" ariaLabel="Status" value={statusValue} onChange={setStatusValue} options={["draft", "active", "closed", "archived"].map((value) => ({ value: value as AssessmentStatus, label: value }))} /></label>
              <label className="grid gap-2 text-sm text-white/60">AI assistance<CustomDropdown name="ai_enabled" ariaLabel="AI assistance" value={aiAccess} onChange={setAiAccess} options={[{ value: "enabled", label: "enabled" }, { value: "disabled", label: "disabled" }]} /></label>
            </div>
            <div className="flex flex-wrap gap-3">
              <button className="btn-primary" disabled={isSaving || isDeleting}>
                {isSaving ? <Loader2 className="animate-spin" size={16} /> : null}
                {isSaving ? "Saving..." : "Save changes"}
              </button>
              <button className="btn-secondary text-pinkGlow" type="button" disabled={isSaving || isDeleting} onClick={handleDeleteAssessment}>
                {isDeleting ? <Loader2 className="animate-spin" size={16} /> : <Trash2 size={16} />}
                {isDeleting ? "Deleting..." : "Delete assessment"}
              </button>
            </div>
            {saved ? <p className="text-sm text-cyanGlow">Changes saved.</p> : null}
            {error ? <p className="text-sm text-pinkGlow">{error}</p> : null}
          </form>
        </section>
        <QuestionTestCaseEditor assessment={assessment} onAssessmentChange={setAssessment} />
      </div>
    </div>
  );
}
